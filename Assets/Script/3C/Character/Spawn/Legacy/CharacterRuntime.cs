using System;
using UnityEngine;

namespace CGame
{
    /// <summary>
    /// 一次角色生成对应的生命周期句柄。释放顺序与装配顺序严格相反。
    /// </summary>
    public sealed class CharacterRuntime : IDisposable
    {
        private readonly GameObject root;
        private readonly Pawn pawn;
        private readonly PawnHost pawnHost;
        private readonly CharacterPhysicsMotor motor;
        private readonly PlayerController controller;
        private readonly PawnManager pawnManager;
        private readonly ControllerManager controllerManager;
        private bool isDisposed;

        internal CharacterRuntime(
            GameObject root,
            Pawn pawn,
            PawnHost pawnHost,
            CharacterPhysicsMotor motor,
            PlayerController controller,
            PawnManager pawnManager,
            ControllerManager controllerManager)
        {
            this.root = root;
            this.pawn = pawn;
            this.pawnHost = pawnHost;
            this.motor = motor;
            this.controller = controller;
            this.pawnManager = pawnManager;
            this.controllerManager = controllerManager;
        }

        public Transform Transform => root == null ? null : root.transform;
        public bool IsDisposed => isDisposed;

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            if (root != null)
            {
                root.SetActive(false);
            }

            controller?.SettingInputHandle(null);
            controllerManager?.UnregisteringController(controller);
            pawnManager?.UnregisteringPawn(pawn);
            pawnHost?.UnbindingPawn();
            if (motor != null)
            {
                motor.CharacterController = null;
            }

            if (root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
