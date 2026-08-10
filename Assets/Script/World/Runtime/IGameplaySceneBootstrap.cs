using UnityEngine;

namespace CGame
{
    public interface IGameplaySceneBootstrap
    {
        WorldStartResult StartScene(World world, WorldStartResult startResult);

        bool TryGetPlayerCameraPose(out Vector3 position, out Quaternion rotation);
    }
}
