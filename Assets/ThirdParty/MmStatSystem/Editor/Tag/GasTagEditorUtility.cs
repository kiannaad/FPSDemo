using System.Collections.Generic;
using System.Text;
using GAS.TagSystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Tag
{
    /// <summary>
    /// Tag 编辑器工具 查找库 校验 读写列表
    /// </summary>
    public static class GasTagEditorUtility
    {
        /// <summary> 序列化字段名 </summary>
        public const string TagListPropertyName = "tagList";

        /// <summary>
        /// 查找或加载标签库
        /// </summary>
        public static GameplayTagDatabase FindDatabase()
        {
            GameplayTagDatabase db = GameplayTagDatabase.Get();
            if (db != null) return db;

            string[] guidList = AssetDatabase.FindAssets("t:GameplayTagDatabase");
            if (guidList.Length == 0) return null;

            string path = AssetDatabase.GUIDToAssetPath(guidList[0]);
            db = AssetDatabase.LoadAssetAtPath<GameplayTagDatabase>(path);
            return db;
        }

        /// <summary>
        /// 确保存在标签库 没有则创建到 Resources
        /// </summary>
        public static GameplayTagDatabase EnsureDatabase()
        {
            GameplayTagDatabase db = FindDatabase();
            if (db != null) return db;

            const string folderPath = "Assets/Resources/GAS";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/Resources", "GAS");

            db = ScriptableObject.CreateInstance<GameplayTagDatabase>();
            AssetDatabase.CreateAsset(db, folderPath + "/GameplayTagDatabase.asset");
            AssetDatabase.SaveAssets();
            GameplayTagDatabase.ClearCache();
            return db;
        }

        /// <summary>
        /// 获取全部标签名列表
        /// </summary>
        public static List<string> GetTagNameList()
        {
            List<string> resultList = new List<string>();
            GameplayTagDatabase db = FindDatabase();
            if (db == null || db.Tags == null) return resultList;

            for (int i = 0; i < db.Tags.Count; i++)
            {
                string tag = db.Tags[i];
                if (!string.IsNullOrWhiteSpace(tag))
                    resultList.Add(tag);
            }

            resultList.Sort(System.StringComparer.OrdinalIgnoreCase);
            return resultList;
        }

        /// <summary>
        /// 校验标签名 返回错误信息 空表示合法
        /// </summary>
        public static string ValidateTagName(string tagName, GameplayTagDatabase db, string ignoreOldName = null)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return "标签名不能为空";

            tagName = tagName.Trim();
            if (tagName.Contains(" "))
                return "标签名不能包含空格";

            if (tagName.StartsWith(".") || tagName.EndsWith("."))
                return "标签名不能以点开头或结尾";

            if (tagName.Contains(".."))
                return "标签名不能包含连续的点";

            if (db != null && db.Tags != null)
            {
                for (int i = 0; i < db.Tags.Count; i++)
                {
                    string existing = db.Tags[i];
                    if (string.IsNullOrWhiteSpace(existing)) continue;
                    if (ignoreOldName != null && existing == ignoreOldName) continue;
                    if (string.Equals(existing, tagName, System.StringComparison.OrdinalIgnoreCase))
                        return "标签已存在";
                }
            }

            return null;
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        public static bool TryAddTag(GameplayTagDatabase db, string tagName, out string error)
        {
            error = ValidateTagName(tagName, db);
            if (error != null) return false;

            SerializedObject so = new SerializedObject(db);
            SerializedProperty listProp = so.FindProperty(TagListPropertyName);
            listProp.arraySize++;
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = tagName.Trim();
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
            GameplayTagDatabase.ClearCache();
            return true;
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public static bool TryRemoveTag(GameplayTagDatabase db, string tagName)
        {
            if (db == null || string.IsNullOrWhiteSpace(tagName)) return false;

            SerializedObject so = new SerializedObject(db);
            SerializedProperty listProp = so.FindProperty(TagListPropertyName);
            for (int i = listProp.arraySize - 1; i >= 0; i--)
            {
                if (listProp.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    listProp.DeleteArrayElementAtIndex(i);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(db);
                    GameplayTagDatabase.ClearCache();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 重命名标签
        /// </summary>
        public static bool TryRenameTag(GameplayTagDatabase db, string oldName, string newName, out string error)
        {
            error = ValidateTagName(newName, db, oldName);
            if (error != null) return false;

            SerializedObject so = new SerializedObject(db);
            SerializedProperty listProp = so.FindProperty(TagListPropertyName);
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                if (element.stringValue == oldName)
                {
                    element.stringValue = newName.Trim();
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(db);
                    GameplayTagDatabase.ClearCache();
                    return true;
                }
            }

            error = "未找到原标签";
            return false;
        }

        /// <summary>
        /// 构建树形缩进显示名
        /// </summary>
        public static string BuildTreeLabel(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return tagName;
            string[] partList = tagName.Split('.');
            if (partList.Length <= 1) return tagName;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < partList.Length - 1; i++)
                sb.Append("    ");
            sb.Append(partList[partList.Length - 1]);
            sb.Append("  (").Append(tagName).Append(')');
            return sb.ToString();
        }
    }
}
