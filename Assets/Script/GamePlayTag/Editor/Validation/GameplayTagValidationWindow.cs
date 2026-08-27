using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public sealed class GameplayTagValidationWindow : EditorWindow
    {
        private GameplayTagConfig config;
        private GameplayTagSource renameSource;
        private string oldSubtreeName = string.Empty;
        private string newSubtreeName = string.Empty;
        private string operationMessage = string.Empty;
        private GameplayTagRenamePlan renamePlan;
        private GameplayTagAssetScanReport scanReport;
        private Vector2 scrollPosition;
        private readonly HashSet<int> selectedIndices = new HashSet<int>();

        [MenuItem("CGame/Gameplay Tags/Validation & Migration")]
        public static void Open()
        {
            GameplayTagValidationWindow window = GetWindow<GameplayTagValidationWindow>();
            window.titleContent = new GUIContent("Tag Validation");
            window.minSize = new Vector2(680f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshConfig();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("GameplayTag Change Safety", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Rename first creates a validated Source/Redirect plan. Asset migration then writes resolvable references and rescans every saved asset.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Authoritative Config", config, typeof(GameplayTagConfig), false);
            }

            DrawRenameSection();
            EditorGUILayout.Space(8f);
            DrawValidationSection();
            if (!string.IsNullOrEmpty(operationMessage))
            {
                EditorGUILayout.HelpBox(operationMessage, MessageType.None);
            }
        }

        private void DrawRenameSection()
        {
            EditorGUILayout.LabelField("Subtree Rename", EditorStyles.boldLabel);
            renameSource = (GameplayTagSource)EditorGUILayout.ObjectField(
                "Source",
                renameSource,
                typeof(GameplayTagSource),
                false);
            oldSubtreeName = EditorGUILayout.TextField("Old Subtree", oldSubtreeName);
            newSubtreeName = EditorGUILayout.TextField("New Subtree", newSubtreeName);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Rename"))
            {
                PreviewRename();
            }

            using (new EditorGUI.DisabledScope(renamePlan == null))
            {
                if (GUILayout.Button("Apply Rename"))
                {
                    ApplyRename();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (renamePlan != null)
            {
                EditorGUILayout.LabelField($"Redirects to add: {renamePlan.Mappings.Count}");
                foreach (GameplayTagRenameMapping mapping in renamePlan.Mappings.Take(8))
                {
                    EditorGUILayout.LabelField($"  {mapping.OldName}  ->  {mapping.NewName}", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField("Asset References", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate All"))
            {
                ValidateAll();
            }

            using (new EditorGUI.DisabledScope(scanReport == null || selectedIndices.Count == 0))
            {
                if (GUILayout.Button("Fix Selected"))
                {
                    FixSelected();
                }
            }

            using (new EditorGUI.DisabledScope(scanReport == null ||
                                                !scanReport.References.Any(reference =>
                                                    reference.Status == GameplayTagReferenceStatus.NeedsMigration)))
            {
                if (GUILayout.Button("Fix All Resolvable"))
                {
                    FixAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (scanReport == null)
            {
                return;
            }

            int registeredCount = scanReport.References.Count(reference => reference.Status == GameplayTagReferenceStatus.Registered);
            int migrationCount = scanReport.References.Count(reference => reference.Status == GameplayTagReferenceStatus.NeedsMigration);
            int invalidCount = scanReport.References.Count(reference => reference.Status == GameplayTagReferenceStatus.Unregistered);
            EditorGUILayout.LabelField($"Registered {registeredCount}    Needs migration {migrationCount}    Invalid {invalidCount}");

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(180f));
            for (int index = 0; index < scanReport.References.Count; index++)
            {
                GameplayTagAssetReference reference = scanReport.References[index];
                if (reference.Status == GameplayTagReferenceStatus.Registered)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                bool selected = selectedIndices.Contains(index);
                bool nextSelected = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
                if (nextSelected != selected)
                {
                    if (nextSelected)
                    {
                        selectedIndices.Add(index);
                    }
                    else
                    {
                        selectedIndices.Remove(index);
                    }
                }

                string destination = string.IsNullOrEmpty(reference.ResolvedName) ? "<unresolved>" : reference.ResolvedName;
                EditorGUILayout.LabelField(
                    $"[{reference.Status}] {reference.AssetPath}  {reference.PropertyPath}  {reference.RawName} -> {destination}");
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void PreviewRename()
        {
            renamePlan = GameplayTagRenameService.TryCreatePlan(
                config,
                renameSource,
                oldSubtreeName,
                newSubtreeName,
                out GameplayTagRenamePlan plan,
                out string error)
                ? plan
                : null;
            operationMessage = renamePlan == null
                ? error
                : $"Preview is valid. {renamePlan.Mappings.Count} explicit redirect(s) will be added.";
        }

        private void ApplyRename()
        {
            if (!EditorUtility.DisplayDialog(
                    "Apply GameplayTag Rename",
                    $"Apply {renamePlan.Mappings.Count} explicit redirect(s) and mutate Source '{renamePlan.Source.SourceName}'?",
                    "Apply Rename",
                    "Cancel"))
            {
                return;
            }

            bool applied = GameplayTagRenameService.Apply(renamePlan, out string error);
            operationMessage = applied ? "Rename applied. Validate and migrate asset references next." : error;
            renamePlan = null;
            if (applied)
            {
                ValidateAll();
            }
        }

        private void ValidateAll()
        {
            selectedIndices.Clear();
            if (!GameplayTagValidationGate.ValidateProject(out scanReport, out string error))
            {
                operationMessage = $"Validation found blocking issues. {error}";
            }
            else
            {
                operationMessage = "Validation passed with no unmigrated or invalid GameplayTag references.";
            }
        }

        private void FixSelected()
        {
            GameplayTagAssetMigrationResult result = GameplayTagAssetMigrationService.FixSelected(
                config,
                selectedIndices
                    .Where(index => index >= 0 && index < scanReport.References.Count)
                    .Select(index => scanReport.References[index]));
            CompleteMigration(result);
        }

        private void FixAll()
        {
            CompleteMigration(GameplayTagAssetMigrationService.FixAll(config, scanReport));
        }

        private void CompleteMigration(GameplayTagAssetMigrationResult result)
        {
            selectedIndices.Clear();
            scanReport = result.Rescan;
            operationMessage = result.Errors.Count == 0
                ? $"Saved {result.SavedAssets.Count} asset(s); rescan completed."
                : string.Join("\n", result.Errors);
            ValidateAll();
        }

        private void RefreshConfig()
        {
            if (!GameplayTagConfigLocator.TryLoadUnique(out config, out string error))
            {
                config = null;
                operationMessage = error;
            }
        }
    }
}
