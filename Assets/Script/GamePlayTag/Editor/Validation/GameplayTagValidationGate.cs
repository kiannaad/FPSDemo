using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public static class GameplayTagValidationGate
    {
        public static bool ValidateProject(out GameplayTagAssetScanReport report, out string error)
        {
            GameplayTagPlayModeValidationCache.MarkUnvalidated();
            if (!GameplayTagConfigLocator.TryLoadUnique(out GameplayTagConfig config, out error))
            {
                report = new GameplayTagAssetScanReport(Array.Empty<GameplayTagAssetReference>(), new[] { error });
                return false;
            }

            report = GameplayTagAssetScanner.ScanProject(config);
            GameplayTagValidationState.ReplaceWith(report);
            if (!report.HasBlockingIssues)
            {
                GameplayTagPlayModeValidationCache.MarkValidated();
                error = string.Empty;
                return true;
            }

            string configuration = string.Join("; ", report.ConfigurationErrors);
            string references = string.Join(
                "; ",
                report.References
                    .Where(reference => reference.Status != GameplayTagReferenceStatus.Registered)
                    .Take(8)
                    .Select(reference => $"{reference.AssetPath}:{reference.PropertyPath}='{reference.RawName}'"));
            error = string.IsNullOrEmpty(configuration) ? references : configuration;
            return false;
        }

        public static bool ValidateForPlayMode(out string error)
        {
            if (!GameplayTagPlayModeValidationCache.RequiresValidation)
            {
                error = string.Empty;
                return true;
            }

            return ValidateProject(out _, out error);
        }
    }

    public static class GameplayTagPlayModeValidationCache
    {
        private const string RevisionKey = "CGame.GameplayTags.Validation.Revision";
        private const string ValidatedRevisionKey = "CGame.GameplayTags.Validation.ValidatedRevision";
        private const string GameplayTagScriptRoot = "Assets/Script/GamePlayTag/";

        public static bool RequiresValidation =>
            SessionState.GetInt(ValidatedRevisionKey, -1) !=
            SessionState.GetInt(RevisionKey, 0);

        public static void MarkValidated()
        {
            SessionState.SetInt(
                ValidatedRevisionKey,
                SessionState.GetInt(RevisionKey, 0));
        }

        public static void MarkUnvalidated()
        {
            SessionState.EraseInt(ValidatedRevisionKey);
        }

        public static bool InvalidateForPaths(IEnumerable<string> paths)
        {
            if (!(paths ?? Array.Empty<string>()).Any(IsValidationInputPath))
            {
                return false;
            }

            int revision = SessionState.GetInt(RevisionKey, 0);
            SessionState.SetInt(RevisionKey, unchecked(revision + 1));
            return true;
        }

        public static void Clear()
        {
            SessionState.EraseInt(RevisionKey);
            SessionState.EraseInt(ValidatedRevisionKey);
        }

        private static bool IsValidationInputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (path.StartsWith(GameplayTagScriptRoot, StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.StartsWith("Assets/Tests/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets/ThirdParty/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }
    }

    [InitializeOnLoad]
    internal static class GameplayTagPlayModeGate
    {
        static GameplayTagPlayModeGate()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || GameplayTagValidationGate.ValidateForPlayMode(out string error))
            {
                return;
            }

            EditorApplication.isPlaying = false;
            Debug.LogError($"GameplayTag validation blocked Play Mode: {error}");
        }
    }

    public sealed class GameplayTagBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!GameplayTagValidationGate.ValidateProject(out _, out string error))
            {
                throw new BuildFailedException($"GameplayTag validation blocked Player build: {error}");
            }
        }
    }
}
