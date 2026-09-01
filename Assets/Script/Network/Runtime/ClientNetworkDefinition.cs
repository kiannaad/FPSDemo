using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "ClientNetwork", menuName = "CGame/Network/Client Network")]
    public sealed class ClientNetworkDefinition : ScriptableObject
    {
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 29000;
        [SerializeField] private string connectionKey = "fps-v1";
        [SerializeField] private float requestTimeoutSeconds = 5f;

        public string Host => host;
        public int Port => port;
        public string ConnectionKey => connectionKey;
        public float RequestTimeoutSeconds => requestTimeoutSeconds;
    }
}
