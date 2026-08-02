using UnityEngine;

namespace CGame.GameplayTags.Tests.Fixtures
{
    public sealed class GameplayTagAuthoringFixtureComponent : MonoBehaviour
    {
        [SerializeField] private GameplayTag singleTag;
        [SerializeField] private GameplayTag emptyTag;
        [SerializeField] private GameplayTag unregisteredTag;
        [SerializeField] private GameplayTagContainer tags = new GameplayTagContainer();

        public GameplayTag SingleTag => singleTag;
        public GameplayTag EmptyTag => emptyTag;
        public GameplayTag UnregisteredTag => unregisteredTag;
        public GameplayTagContainer Tags => tags;
    }
}
