using System.Collections.Generic;
using System.Linq;
using CGame.Ability.Cues;
using UnityEditor;
using UnityEngine;

namespace CGame.Editor
{
    public static class GameplayCueBulletHoleAssetInstaller
    {
        private const string CueSetPath = "Assets/Settings/Gameplay/Cues/WeaponGameplayCueSet.asset";
        private const string NotifyPath = "Assets/Settings/Gameplay/Cues/BulletHoleCueNotify.asset";
        private const string TexturePath = "Assets/Art/Textures/WeaponEffects/BulletHole_Black_v1.png";

        [MenuItem("CGame/Gameplay Cue/Install Bullet Hole Impact Notify")]
        public static void Install()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                throw new System.InvalidOperationException($"Bullet-hole texture is missing: {TexturePath}");
            }

            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            GameplayCueSet cueSet = AssetDatabase.LoadAssetAtPath<GameplayCueSet>(CueSetPath);
            if (cueSet == null)
            {
                throw new System.InvalidOperationException($"Weapon CueSet is missing: {CueSetPath}");
            }

            BulletHoleCueNotifyDefinition notify = AssetDatabase.LoadAssetAtPath<BulletHoleCueNotifyDefinition>(NotifyPath);
            if (notify == null)
            {
                notify = ScriptableObject.CreateInstance<BulletHoleCueNotifyDefinition>();
                AssetDatabase.CreateAsset(notify, NotifyPath);
            }

            notify.Configure(texture, 8f, 1f, 0.20f, 0.002f, 24);
            GameplayCueSetEntry impactEntry = cueSet.Entries.FirstOrDefault(entry => entry != null && entry.CueTag.Name == "GameplayCue.Weapon.Impact");
            if (impactEntry == null)
            {
                throw new System.InvalidOperationException("Weapon CueSet has no GameplayCue.Weapon.Impact entry.");
            }

            var notifies = new List<CueNotifyDefinition>(impactEntry.Notifies);
            if (!notifies.Contains(notify))
            {
                notifies.Add(notify);
                impactEntry.SetDefinition(impactEntry.CueTag, notifies.ToArray());
            }

            EditorUtility.SetDirty(notify);
            EditorUtility.SetDirty(cueSet);
            AssetDatabase.SaveAssets();
            Debug.Log($"Installed bullet-hole Cue notify at {NotifyPath}.");
        }
    }
}
