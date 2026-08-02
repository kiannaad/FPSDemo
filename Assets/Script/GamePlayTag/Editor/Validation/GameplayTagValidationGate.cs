using System;
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
            if (!GameplayTagConfigLocator.TryLoadUnique(out GameplayTagConfig config, out error))
            {
                report = new GameplayTagAssetScanReport(Array.Empty<GameplayTagAssetReference>(), new[] { error });
                return false;
            }

            report = GameplayTagAssetScanner.ScanProject(config);
            GameplayTagValidationState.ReplaceWith(report);
            if (!report.HasBlockingIssues)
            {
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
            if (state != PlayModeStateChange.ExitingEditMode || GameplayTagValidationGate.ValidateProject(out _, out string error))
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
