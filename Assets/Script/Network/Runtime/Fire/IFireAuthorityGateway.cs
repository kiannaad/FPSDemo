using System;
using UnityEngine;

namespace CGame.Network
{
    public sealed class OwnerNetworkFireGateway : CGame.IFireAuthorityGateway
    {
        private readonly ClientNetworkSubSystem network;
        private readonly NetworkFireBridge bridge;
        private readonly Func<long> clientTickProvider;

        public OwnerNetworkFireGateway(
            ClientNetworkSubSystem network,
            NetworkFireBridge bridge,
            Func<long> clientTickProvider)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            this.clientTickProvider = clientTickProvider ?? throw new ArgumentNullException(nameof(clientTickProvider));
        }

        public long BeginPredicted(
            long equipmentInstanceId,
            int predictedMagazineAmmo,
            Vector3 origin,
            Vector3 direction)
        {
            FireRequest request = bridge.BeginPrediction(
                equipmentInstanceId,
                predictedMagazineAmmo,
                clientTickProvider(),
                origin.x,
                origin.y,
                origin.z,
                direction.x,
                direction.y,
                direction.z);
            _ = SendAsync(request);
            return request.PredictionNonce;
        }

        private async System.Threading.Tasks.Task SendAsync(FireRequest request)
        {
            try
            {
                await network.SendFireRequestAsync(request);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Network][044] FireRequestTransportFailure PredictionNonce={request.PredictionNonce} Reason={exception.Message}");
            }
        }
    }
}
