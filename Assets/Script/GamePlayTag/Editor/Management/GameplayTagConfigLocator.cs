using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public static class GameplayTagConfigLocator
    {
        public static bool TryLoadUnique(out GameplayTagConfig config, out string error)
        {
            var matches = new List<GameplayTagConfig>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                GameplayTagConfig candidate = prefab == null ? null : prefab.GetComponent<GameplayTagConfig>();
                if (candidate != null)
                {
                    matches.Add(candidate);
                }
            }

            if (matches.Count != 1)
            {
                config = null;
                error = matches.Count == 0
                    ? "No prefab containing GameplayTagConfig was found."
                    : $"Expected one GameplayTagConfig prefab but found {matches.Count}.";
                return false;
            }

            config = matches[0];
            error = string.Empty;
            return true;
        }
    }
}
