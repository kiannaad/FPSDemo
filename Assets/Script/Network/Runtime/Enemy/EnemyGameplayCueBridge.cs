using UnityEngine;

namespace CGame.Network
{
    public interface IEnemyActionCueSink
    {
        bool TryExecute(EnemyActionEvent action, GameObject presentationRoot);
    }
}
