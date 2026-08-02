using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public sealed class GameplayTagManagerWindow : EditorWindow
    {
        private const string DefaultSourceFolder = "Assets/Data/GamePlayTag/Sources";
        private readonly GameplayTagRegistryBuilder builder = new GameplayTagRegistryBuilder();
        private TreeViewState treeState;
        private SearchField searchField;
        private GameplayTagManagerTreeView treeView;
        private GameplayTagConfig config;
        private GameplayTagRegistryBuildResult buildResult;
        private string configError = string.Empty;
        private Vector2 managementScroll;
        private string search = string.Empty;
        private int sourceFilterIndex;
        private int editSourceIndex;
        private string tagPath = string.Empty;
        private string tagComment = string.Empty;
        private string newSourceName = "NewGameplayTags";
        private GameplayTagSource existingSource;
        private string operationMessage = string.Empty;
        private MessageType operationMessageType = MessageType.Info;

        [MenuItem("Window/CGame/Gameplay Tag Manager")]
        public static void Open()
        {
            GetWindow<GameplayTagManagerWindow>("Gameplay Tag Manager");
        }

        private void OnEnable()
        {
            treeState ??= new TreeViewState();
            searchField ??= new SearchField();
            treeView ??= new GameplayTagManagerTreeView(treeState);
            RefreshData();
        }

        private void OnFocus()
        {
            RefreshData();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (config == null)
            {
                EditorGUILayout.HelpBox(configError, MessageType.Error);
                if (GUILayout.Button("Refresh Config"))
                {
                    RefreshData();
                }

                return;
            }

            if (buildResult == null || !buildResult.Succeeded)
            {
                string errors = buildResult == null
                    ? "Registry has not been built."
                    : string.Join("\n", buildResult.Errors.Select(error => error.ToString()));
                EditorGUILayout.HelpBox(errors, MessageType.Error);
            }

            Rect treeRect = GUILayoutUtility.GetRect(0f, 100000f, 180f, position.height * 0.55f);
            treeView.OnGUI(treeRect);
            if (!treeView.HasVisibleRows)
            {
                GUI.Label(treeRect, "No tags match current search/filter.", EditorStyles.centeredGreyMiniLabel);
            }

            DrawAddTagPanel();
            DrawSourceManagement();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string nextSearch = searchField.OnToolbarGUI(search);
                if (!string.Equals(nextSearch, search, StringComparison.Ordinal))
                {
                    search = nextSearch;
                    UpdateTree();
                }

                string[] filters = GetSourceFilters();
                int nextFilter = EditorGUILayout.Popup(sourceFilterIndex, filters, EditorStyles.toolbarPopup, GUILayout.Width(190f));
                if (nextFilter != sourceFilterIndex)
                {
                    sourceFilterIndex = nextFilter;
                    UpdateTree();
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65f)))
                {
                    RefreshData();
                }
            }
        }

        private void DrawAddTagPanel()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Add Explicit Tag", EditorStyles.boldLabel);
            GameplayTagSource[] sources = GetSources();
            string[] names = sources.Select(source => source.SourceName).ToArray();
            using (new EditorGUI.DisabledScope(sources.Length == 0))
            {
                editSourceIndex = Mathf.Clamp(editSourceIndex, 0, Mathf.Max(0, sources.Length - 1));
                editSourceIndex = EditorGUILayout.Popup("Source", editSourceIndex, names);
                tagPath = EditorGUILayout.TextField("Tag Path", tagPath);
                tagComment = EditorGUILayout.TextField("Comment", tagComment);
                if (GUILayout.Button("Add Tag", GUILayout.Height(24f)))
                {
                    if (GameplayTagSourceMutationService.AddTagPath(sources[editSourceIndex], tagPath, tagComment, out string error))
                    {
                        tagPath = string.Empty;
                        tagComment = string.Empty;
                        SetOperation("Tag added.", MessageType.Info);
                        RefreshData();
                    }
                    else
                    {
                        SetOperation(error, MessageType.Error);
                    }
                }
            }

            DrawOperationMessage();
        }

        private void DrawSourceManagement()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Source Management", EditorStyles.boldLabel);
            managementScroll = EditorGUILayout.BeginScrollView(managementScroll, GUILayout.MaxHeight(150f));
            using (new EditorGUILayout.HorizontalScope())
            {
                newSourceName = EditorGUILayout.TextField(newSourceName);
                if (GUILayout.Button("Create Source", GUILayout.Width(115f)))
                {
                    GameplayTagSource created = GameplayTagSourceAssetService.CreateAndRegister(
                        config,
                        newSourceName,
                        DefaultSourceFolder,
                        out string error);
                    SetOperation(created == null ? error : $"Created '{created.SourceName}'.", created == null ? MessageType.Error : MessageType.Info);
                    RefreshData();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                existingSource = (GameplayTagSource)EditorGUILayout.ObjectField(existingSource, typeof(GameplayTagSource), false);
                if (GUILayout.Button("Add Existing", GUILayout.Width(115f)))
                {
                    bool added = GameplayTagSourceAssetService.Register(config, existingSource, out string error);
                    SetOperation(added ? "Source registered." : error, added ? MessageType.Info : MessageType.Error);
                    RefreshData();
                }
            }

            GameplayTagSource[] sources = GetSources();
            if (sources.Length > 0)
            {
                editSourceIndex = Mathf.Clamp(editSourceIndex, 0, sources.Length - 1);
                GameplayTagSource selected = sources[editSourceIndex];
                string[] filters = GetSourceFilters();
                GameplayTagSource filteredSource = sourceFilterIndex > 0 && sourceFilterIndex < filters.Length
                    ? sources.FirstOrDefault(source => source.SourceName.Equals(filters[sourceFilterIndex], StringComparison.OrdinalIgnoreCase))
                    : null;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Remove From Config"))
                    {
                        bool removed = GameplayTagSourceAssetService.Remove(config, selected, out string error);
                        if (removed)
                        {
                            existingSource = selected;
                        }

                        SetOperation(removed ? "Source removed from Config." : error, removed ? MessageType.Info : MessageType.Error);
                        RefreshData();
                    }

                    using (new EditorGUI.DisabledScope(filteredSource == null || string.IsNullOrEmpty(treeView.SelectedTagName)))
                    {
                        if (GUILayout.Button("Delete Selected Subtree"))
                        {
                            string selectedTagName = treeView.SelectedTagName;
                            if (EditorUtility.DisplayDialog(
                                    "Delete GameplayTag Subtree",
                                    $"Delete subtree '{selectedTagName}' from Source '{filteredSource.SourceName}'?",
                                    "Delete Subtree",
                                    "Cancel"))
                            {
                                bool deleted = GameplayTagSourceMutationService.DeleteSubtreeSafely(
                                    config,
                                    filteredSource,
                                    selectedTagName,
                                    out string error);
                                SetOperation(deleted ? "Subtree deleted." : error, deleted ? MessageType.Info : MessageType.Error);
                                RefreshData();
                            }
                        }
                    }

                    using (new EditorGUI.DisabledScope(existingSource == null || config.Sources.Contains(existingSource)))
                    {
                        if (GUILayout.Button("Delete Source Asset") &&
                            EditorUtility.DisplayDialog("Delete GameplayTag Source", $"Delete asset '{existingSource.name}'?", "Delete", "Cancel"))
                        {
                            bool deleted = GameplayTagSourceAssetService.DeleteAsset(config, existingSource, out string error);
                            if (deleted)
                            {
                                existingSource = null;
                            }

                            SetOperation(deleted ? "Source asset deleted." : error, deleted ? MessageType.Info : MessageType.Error);
                            RefreshData();
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawOperationMessage()
        {
            if (!string.IsNullOrEmpty(operationMessage))
            {
                EditorGUILayout.HelpBox(operationMessage, operationMessageType);
            }
        }

        private void RefreshData()
        {
            if (!GameplayTagConfigLocator.TryLoadUnique(out config, out string error))
            {
                buildResult = null;
                configError = error;
                treeView?.SetData(null, search, string.Empty);
                Repaint();
                return;
            }

            configError = string.Empty;
            if (IsConfigLocatorError(operationMessage))
            {
                operationMessage = string.Empty;
            }

            buildResult = builder.Build(config.Sources, config.Redirects);
            sourceFilterIndex = Mathf.Clamp(sourceFilterIndex, 0, GetSourceFilters().Length - 1);
            editSourceIndex = Mathf.Clamp(editSourceIndex, 0, Mathf.Max(0, GetSources().Length - 1));
            UpdateTree();
            Repaint();
        }

        private void UpdateTree()
        {
            string[] filters = GetSourceFilters();
            string sourceFilter = sourceFilterIndex <= 0 || sourceFilterIndex >= filters.Length ? string.Empty : filters[sourceFilterIndex];
            treeView?.SetData(buildResult?.Snapshot, search, sourceFilter);
        }

        private GameplayTagSource[] GetSources()
        {
            return config == null
                ? Array.Empty<GameplayTagSource>()
                : config.Sources.Where(source => source != null)
                    .OrderBy(source => source.SourceName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
        }

        private string[] GetSourceFilters()
        {
            return new[] { "All Sources" }.Concat(GetSources().Select(source => source.SourceName)).ToArray();
        }

        private void SetOperation(string message, MessageType type)
        {
            operationMessage = message;
            operationMessageType = type;
        }

        private static bool IsConfigLocatorError(string message)
        {
            return message == "No prefab containing GameplayTagConfig was found."
                || message.StartsWith("Expected one GameplayTagConfig prefab but found ", StringComparison.Ordinal);
        }
    }
}
