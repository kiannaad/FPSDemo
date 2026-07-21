using UnityEngine;

namespace CGame
{
    public class PawnHost : MonoBehaviour
    {
        private Pawn _pawn;

        public Pawn Pawn => _pawn;
        public Transform MeshRoot;
        public Animator Animator;

        /// <summary>
        /// 绑定纯逻辑 Pawn。
        /// </summary>
        public void BindingPawn(Pawn pawn)
        {
            if (_pawn == pawn)
            {
                return;
            }

            UnbindingPawn();
            _pawn = pawn;
            _pawn?.BindingHost(this);
        }

        /// <summary>
        /// 解除纯逻辑 Pawn 绑定。
        /// </summary>
        public void UnbindingPawn()
        {
            if (_pawn == null)
            {
                return;
            }

            Pawn oldPawn = _pawn;
            _pawn = null;
            oldPawn.UnbindingHost(this);
        }

        /// <summary>
        /// 销毁宿主时解除 Pawn 绑定。
        /// </summary>
        private void OnDestroy()
        {
            UnbindingPawn();
        }
    }
}
