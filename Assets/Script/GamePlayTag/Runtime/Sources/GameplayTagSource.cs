using System.Collections.Generic;
using UnityEngine;

namespace CGame.GameplayTags
{
    [CreateAssetMenu(fileName = "GameplayTagSource", menuName = "CGame/Gameplay Tags/Tag Source")]
    public sealed class GameplayTagSource : ScriptableObject
    {
        [SerializeField] private string sourceName;
        [SerializeReference] private List<GameplayTagSourceNode> roots = new List<GameplayTagSourceNode>();

        public string SourceName => sourceName ?? string.Empty;
        public IReadOnlyList<GameplayTagSourceNode> Roots => roots.AsReadOnly();

        public void SetDefinition(string newSourceName, IEnumerable<GameplayTagSourceNode> newRoots)
        {
            sourceName = newSourceName;
            roots.Clear();
            if (newRoots != null)
            {
                roots.AddRange(newRoots);
            }
        }
    }
}
