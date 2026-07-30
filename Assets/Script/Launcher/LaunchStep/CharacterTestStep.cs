using System;
using UnityEngine;

namespace CGame
{
    public class CharacterTestStep : ILaunchStep
    {
        private const string RuntimeRootName = "[CharacterTestRuntime]";
        private const string LocalPlayerDefinitionId = "local-player";

        private GameObject runtimeRoot;
        private CharacterSpawnManager spawnManager;
        private CharacterSpawnOperation spawnOperation;
        private CameraManager cameraManager;
        private ControllerManager controllerManager;
        private AimRejectionReason aimRejectionOverride;
        private bool hasAimRejectionOverride;
        private bool readyLogged;

        public void Enter()
        {
            _ = GameManager.Instance;
            spawnManager = GameManager.GetManager<CharacterSpawnManager>();
            cameraManager = GameManager.GetManager<CameraManager>();
            controllerManager = GameManager.GetManager<ControllerManager>();
            cameraManager.SettingAimGameplayDecisionProvider(EvaluateAimGameplayDecision);

            runtimeRoot = new GameObject(RuntimeRootName);
            CreatingLight(runtimeRoot.transform);
            CreatingGround(runtimeRoot.transform);

            spawnOperation = spawnManager.BeginSpawn(new CharacterSpawnRequest(
                new CharacterSpawnRequestId(Guid.NewGuid().ToString("N")),
                new CharacterDefinitionId(LocalPlayerDefinitionId),
                CharacterControlKind.LocalPlayer,
                new CharacterSpawnPlacement(Vector3.zero, Quaternion.identity),
                InputType.Player,
                "RuntimeCharacter"));
            readyLogged = false;
        }

        public bool Update()
        {
            if (spawnOperation == null)
            {
                throw new InvalidOperationException("Character spawn operation was not created.");
            }

            if (spawnOperation.State == CharacterSpawnState.Failed)
            {
                throw new InvalidOperationException($"Character spawn failed: {spawnOperation.Error}.");
            }

            if (spawnOperation.State == CharacterSpawnState.CharacterReady && !readyLogged)
            {
                readyLogged = true;
                Debug.Log("[CharacterTest] Runtime ready. Use WASD to move and Space to jump.");
            }

            return false;
        }

        public void Exit()
        {
            cameraManager?.SettingAimGameplayDecisionProvider(null);
            cameraManager = null;
            controllerManager = null;
            hasAimRejectionOverride = false;
            aimRejectionOverride = AimRejectionReason.None;
            if (spawnManager != null && spawnOperation != null)
            {
                if (spawnOperation.State == CharacterSpawnState.CharacterReady)
                {
                    spawnManager.Despawn(spawnOperation.Result.RuntimeId, CharacterDespawnReason.Requested);
                }
                else if (!spawnOperation.IsComplete || spawnOperation.State == CharacterSpawnState.CancelRequested)
                {
                    spawnManager.CancelSpawn(spawnOperation.Request.RequestId);
                }
            }

            spawnOperation = null;
            readyLogged = false;
            if (runtimeRoot != null)
            {
                UnityEngine.Object.Destroy(runtimeRoot);
                runtimeRoot = null;
            }
        }

        public void SettingAimRejectionOverride(AimRejectionReason rejectionReason)
        {
            if (rejectionReason == AimRejectionReason.None ||
                rejectionReason == AimRejectionReason.IntentReleased)
            {
                throw new ArgumentException("The CharacterTest aim authority requires an explicit gameplay rejection.",
                    nameof(rejectionReason));
            }

            aimRejectionOverride = rejectionReason;
            hasAimRejectionOverride = true;
        }

        public void ClearingAimRejectionOverride()
        {
            hasAimRejectionOverride = false;
            aimRejectionOverride = AimRejectionReason.None;
        }

        private AimGameplayDecision EvaluateAimGameplayDecision(bool aimHeld)
        {
            if (!aimHeld)
            {
                return AimGameplayDecision.Released;
            }

            if (hasAimRejectionOverride)
            {
                return AimGameplayDecision.Rejected(aimRejectionOverride);
            }

            WeaponRuntime weaponRuntime = controllerManager
                ?.GettingController<PlayerController>()
                ?.WeaponRuntime;
            if (weaponRuntime == null
                || !weaponRuntime.IsInitialized
                || !weaponRuntime.Snapshot.IsEquipped)
            {
                return AimGameplayDecision.Rejected(
                    AimRejectionReason.NoWeapon);
            }

            if (weaponRuntime.IsSwitching)
            {
                return AimGameplayDecision.Rejected(
                    AimRejectionReason.WeaponSwitching);
            }

            if (!weaponRuntime.Capabilities.SupportsFire)
            {
                return AimGameplayDecision.Rejected(
                    AimRejectionReason.NoWeapon);
            }

            if (weaponRuntime.ActiveAction.IsValid
                && weaponRuntime.ActiveAction.Kind
                    == WeaponActionKind.Reload)
            {
                return AimGameplayDecision.Rejected(
                    AimRejectionReason.Reloading);
            }

            return AimGameplayDecision.Allowed;
        }

        private static void CreatingGround(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
        }

        private static void CreatingLight(Transform parent)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }
    }
}
