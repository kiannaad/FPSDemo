using UnityEngine;

namespace CGame
{
    [DisallowMultipleComponent]
    public sealed class PlayerStart : MonoBehaviour
    {
        [SerializeField] private string playerStartId = "LocalPlayer";

        public PlayerStartInfo GetInfo()
        {
            return new PlayerStartInfo(playerStartId, transform.position, transform.rotation);
        }
    }
}
