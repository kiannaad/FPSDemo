using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace CGame.GameplayTags.Editor
{
    public sealed class GameplayTagPickerModel
    {
        private static GameplayTagPickerModel cachedEditModeModel;
        private static string cachedEditModeError = string.Empty;
        private static bool isEditModeCacheValid;

        private readonly string[] names;
        private readonly Dictionary<string, string> canonicalNames;

        public GameplayTagPickerModel(IEnumerable<GameplayTag> explicitTags)
        {
            names = (explicitTags ?? Array.Empty<GameplayTag>())
                .Where(tag => !tag.IsEmpty)
                .Select(tag => tag.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(name => name, StringComparer.Ordinal)
                .ToArray();
            canonicalNames = names.ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<string> Names => names;

        public IReadOnlyList<string> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return names;
            }

            string term = query.Trim();
            return names.Where(name => name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        }

        public bool TryGetCanonicalName(string value, out string canonicalName)
        {
            if (string.IsNullOrEmpty(value))
            {
                canonicalName = string.Empty;
                return true;
            }

            return canonicalNames.TryGetValue(value, out canonicalName);
        }

        public bool IsExplicit(string value)
        {
            return !string.IsNullOrEmpty(value) && canonicalNames.ContainsKey(value);
        }

        public static bool TryCreateFromAuthoritativeConfig(out GameplayTagPickerModel model, out string error)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (isEditModeCacheValid)
                {
                    model = cachedEditModeModel;
                    error = cachedEditModeError;
                    return model != null;
                }

                if (!GameplayTagConfigLocator.TryLoadUnique(out GameplayTagConfig config, out error))
                {
                    model = null;
                    CacheEditModeResult(model, error);
                    return false;
                }

                GameplayTagRegistryBuildResult result = new GameplayTagRegistryBuilder().Build(config.Sources, config.Redirects);
                if (!result.Succeeded)
                {
                    model = null;
                    error = string.Join("\n", result.Errors.Select(item => item.ToString()));
                    CacheEditModeResult(model, error);
                    return false;
                }

                model = new GameplayTagPickerModel(result.Snapshot.ExplicitTags);
                error = string.Empty;
                CacheEditModeResult(model, error);
                return true;
            }

            if (!GameplayTagManager.Instance.IsInitialized)
            {
                model = null;
                error = "GameplayTagManager is not initialized.";
                return false;
            }

            model = new GameplayTagPickerModel(GameplayTagManager.Instance.GetExplicitTags());
            error = string.Empty;
            return true;
        }

        public static void InvalidateAuthoritativeConfigCache()
        {
            isEditModeCacheValid = false;
            cachedEditModeModel = null;
            cachedEditModeError = string.Empty;

        }

        private static void CacheEditModeResult(GameplayTagPickerModel model, string error)
        {
            cachedEditModeModel = model;
            cachedEditModeError = error ?? string.Empty;
            isEditModeCacheValid = true;
        }
    }
}
