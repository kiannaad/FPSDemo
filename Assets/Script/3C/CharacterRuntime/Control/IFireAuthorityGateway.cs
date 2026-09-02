using UnityEngine;

namespace CGame
{
    public interface IFireAuthorityGateway
    {
        long BeginPredicted(
            long equipmentInstanceId,
            int predictedMagazineAmmo,
            Vector3 origin,
            Vector3 direction);
    }
}
