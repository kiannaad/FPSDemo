using System;
using CGame.Animation;
using UnityEngine;
using YooAsset;

namespace CGame
{
    internal sealed class OwnedCharacterRuntime : IDisposable
    {
        private GameObject root;
        private Character character;
        private PawnHost pawnHost;
        private CharacterPhysicsMotor motor;
        private ICharacterControllerBinding controllerBinding;
        private IPawnRegistration pawnRegistration;
        private AssetHandle definitionHandle;

        public OwnedCharacterRuntime(
            GameObject root,
            Character character,
            PawnHost pawnHost,
            CharacterPhysicsMotor motor,
            ICharacterControllerBinding controllerBinding,
            IPawnRegistration pawnRegistration,
            AssetHandle definitionHandle)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.character =
                character
                ?? throw new ArgumentNullException(nameof(character));
            this.pawnHost = pawnHost ?? throw new ArgumentNullException(nameof(pawnHost));
            this.motor = motor ?? throw new ArgumentNullException(nameof(motor));
            this.controllerBinding = controllerBinding ?? throw new ArgumentNullException(nameof(controllerBinding));
            this.pawnRegistration = pawnRegistration ?? throw new ArgumentNullException(nameof(pawnRegistration));
            this.definitionHandle =
                definitionHandle
                ?? throw new ArgumentNullException(
                    nameof(definitionHandle));
        }

        public Transform Transform => root == null ? null : root.transform;
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            if (root != null)
            {
                root.SetActive(false);
            }

            controllerBinding?.Dispose();
            controllerBinding = null;
            pawnRegistration?.Dispose();
            pawnRegistration = null;
            definitionHandle?.Release();
            definitionHandle = null;
            character?.ShuttingDownPawn();
            character = null;
            pawnHost?.UnbindingPawn();
            pawnHost = null;
            if (motor != null)
            {
                motor.CharacterController = null;
                motor = null;
            }

            if (root == null)
            {
                return;
            }

            GameObject releasedRoot = root;
            root = null;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(releasedRoot);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(releasedRoot);
            }
        }
    }
}
