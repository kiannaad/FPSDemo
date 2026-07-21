using System;
using UnityEngine;

namespace CGame
{
    internal sealed class OwnedCharacterRuntime : IDisposable
    {
        private GameObject root;
        private PawnHost pawnHost;
        private CharacterPhysicsMotor motor;
        private ICharacterControllerBinding controllerBinding;
        private IPawnRegistration pawnRegistration;
        private ResolvedCharacterDefinitionLease definitionLease;

        public OwnedCharacterRuntime(
            GameObject root,
            PawnHost pawnHost,
            CharacterPhysicsMotor motor,
            ICharacterControllerBinding controllerBinding,
            IPawnRegistration pawnRegistration,
            ResolvedCharacterDefinitionLease definitionLease)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.pawnHost = pawnHost ?? throw new ArgumentNullException(nameof(pawnHost));
            this.motor = motor ?? throw new ArgumentNullException(nameof(motor));
            this.controllerBinding = controllerBinding ?? throw new ArgumentNullException(nameof(controllerBinding));
            this.pawnRegistration = pawnRegistration ?? throw new ArgumentNullException(nameof(pawnRegistration));
            this.definitionLease = definitionLease ?? throw new ArgumentNullException(nameof(definitionLease));
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
            definitionLease?.Dispose();
            definitionLease = null;
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
