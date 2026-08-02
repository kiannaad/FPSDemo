using System.Collections.Generic;
using UnityEngine;

namespace GAS.TagSystem
{
    /// <summary>
    /// 全局标签库 运行时只负责存数据和查询
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayTagDatabase", menuName = "GAS/GameplayTagDatabase")]
    public class GameplayTagDatabase : ScriptableObject
    {
        /// <summary> Resources 加载路径 </summary>
        public const string ResourcesPath = "GAS/GameplayTagDatabase";

        /// <summary> 缓存实例 </summary>
        private static GameplayTagDatabase sCached;

        [SerializeField]
        private List<string> tagList = new List<string>();

        /// <summary> 只读标签列表 </summary>
        public IReadOnlyList<string> Tags => tagList;

        /// <summary>
        /// 获取标签库实例
        /// </summary>
        public static GameplayTagDatabase Get()
        {
            if (sCached != null) return sCached;
            sCached = Resources.Load<GameplayTagDatabase>(ResourcesPath);
            return sCached;
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public static void ClearCache()
        {
            sCached = null;
        }

        /// <summary>
        /// 枚举全部有效标签名
        /// </summary>
        public static IEnumerable<string> GetAllTagNames()
        {
            GameplayTagDatabase db = Get();
            if (db == null || db.tagList == null) yield break;

            for (int i = 0; i < db.tagList.Count; i++)
            {
                string tag = db.tagList[i];
                if (!string.IsNullOrWhiteSpace(tag))
                    yield return tag;
            }
        }

        /// <summary>
        /// 标签是否已注册
        /// </summary>
        public bool Contains(string tagName)
        {
            return tagList != null && tagList.Contains(tagName);
        }
    }
}
