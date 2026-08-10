using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability;
using CGame.InventoryEquipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YooAsset;

namespace CGame.PawnRuntime.PlayMode.Tests
{
    public sealed class InventoryEquipmentPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResourceAndAssetServices_LoadReleaseAndDestroyPackage()
        {
            var resources = new ResourceService();
            Task initialization = resources.InitializeAsync();
            while (!initialization.IsCompleted)
            {
                yield return null;
            }

            if (initialization.IsFaulted)
            {
                throw initialization.Exception;
            }

            var assets = new AssetService(resources);
            const string location = "KnifeWeaponAnimationDefinition";
            Assert.That(assets.CheckLocation(location), Is.True);
            AssetHandle handle = assets.LoadAsset<ScriptableObject>(location);
            while (!handle.IsDone)
            {
                yield return null;
            }

            Assert.That(handle.Status, Is.EqualTo(EOperationStatus.Succeed), handle.LastError);
            Assert.That(handle.AssetObject, Is.Not.Null);
            handle.Dispose();
            assets.UnloadUnusedAssets();

            Task shutdown = resources.ShutdownAsync();
            while (!shutdown.IsCompleted)
            {
                yield return null;
            }

            if (shutdown.IsFaulted)
            {
                throw shutdown.Exception;
            }

            Assert.That(resources.IsReady, Is.False);
            Assert.That(YooAssets.ContainsPackage("DefaultPackage"), Is.False);
        }

        [UnityTest]
        public IEnumerator WorldServicesDriveMovementCameraAndWeaponActionsWithoutLegacyFramework()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            prefab.name = "CutoverPawnPrefab";
            prefab.SetActive(false);
            PawnData pawnData = PawnData.CreateRuntime(prefab, new AbilitySet());
            WeaponEquipmentDefinition equipment = WeaponEquipmentDefinition.CreateRuntime(10, 1);
            WeaponEquipmentDefinition sidearmEquipment = WeaponEquipmentDefinition.CreateRuntime(6, 1);
            WeaponItemDefinition itemDefinition = WeaponItemDefinition.CreateRuntime(equipment, 2, 8);
            WeaponItemDefinition sidearmItem = WeaponItemDefinition.CreateRuntime(sidearmEquipment, 2, 4);
            InitialInventorySet initialSet = InitialInventorySet.CreateRuntime(
                0,
                itemDefinition,
                sidearmItem);
            PossessionControllerDefinition controllerDefinition =
                ScriptableObject.CreateInstance<PossessionControllerDefinition>();
            EquipmentPlayModeGameModeDefinition gameModeDefinition =
                ScriptableObject.CreateInstance<EquipmentPlayModeGameModeDefinition>();
            gameModeDefinition.Configure(pawnData, controllerDefinition, initialSet);
            var input = new CutoverInputService();
            CharacterPhysicsSettings physicsSettings = ScriptableObject.CreateInstance<CharacterPhysicsSettings>();
            physicsSettings.Interpolate = false;
            GameObject worldHost = new GameObject("WorldBehaviour.LegacyCutoverTest");
            WorldBehaviour behaviour = worldHost.AddComponent<WorldBehaviour>();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = Vector3.zero;
            GameObject cameraRoot = new GameObject("CutoverEvidenceCamera");
            Camera camera = cameraRoot.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.1f);
            GameObject lightRoot = new GameObject("CutoverEvidenceLight");
            Light light = lightRoot.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightRoot.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            string evidenceDirectory = Environment.GetEnvironmentVariable(
                "CGAME_CUTOVER_VISUAL_EVIDENCE_DIR");

            try
            {
                Task<WorldStartResult> startTask = behaviour.StartWorld(
                    new IWorldCoreService[] { input },
                    new GameLauncher(new Func<ILaunchStep>[] { () => new EquipmentReadyStep() }),
                    new CharacterPhysicsWorld(physicsSettings, ownsSettings: true));
                while (!startTask.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(startTask.Result.Succeeded, Is.True);
                Assert.That(behaviour.RuntimeWorld.StartGame(
                    new GameplaySessionFactory(gameModeDefinition)).Succeeded, Is.True);
                GameMode gameMode = ((GameManager)behaviour.RuntimeWorld.CurrentGameSession).CurrentGameMode;
                PlayerController controller = gameMode.LocalPlayerController;
                var starts = new PlayerStartRegistry();
                starts.Register(new PlayerStartInfo("local", Vector3.up, Quaternion.identity));
                starts.Register(new PlayerStartInfo("respawn", Vector3.up + Vector3.right, Quaternion.identity));
                Assert.That(gameMode.PrepareLocalPawn(new PawnFactory(), starts), Is.True);
                Assert.That(gameMode.CommitLocalPawn(), Is.True);
                yield return null;
                yield return null;
                yield return null;

                PawnAssembly pawn = gameMode.CurrentPawnAssembly;
                Vector3 initialPosition = pawn.Root.transform.position;
                input.Intent = new CharacterControlIntent(Vector3.forward, false, true);
                input.LookDelta = new Vector2(1f, 0f);
                for (int frame = 0; frame < 30; frame++)
                {
                    yield return null;
                }

                Assert.That(pawn.Root.transform.position.z, Is.GreaterThan(initialPosition.z + 0.01f));
                Assert.That(Mathf.Abs(controller.ControlRotation.eulerAngles.y), Is.GreaterThan(0.1f));
                var cameraRuntime = controller.PlayerCamera as IPlayerCameraRuntime;
                Assert.That(cameraRuntime, Is.Not.Null);
                Assert.That(cameraRuntime.Position.y,
                    Is.EqualTo(pawn.Root.transform.position.y + 1.6f).Within(0.01f));
                PositionEvidenceCamera(cameraRoot.transform, cameraRuntime, pawn.Root.transform);
                pawn.Root.GetComponent<Renderer>().material.color = new Color(0.15f, 0.65f, 1f);
                yield return Capture(evidenceDirectory, "movement-camera.png");

                WeaponInstance weapon = pawn.Equipment.CurrentWeapon;
                int ammoBeforeFire = weapon.Item.MagazineAmmo;
                input.FirePressed = true;
                yield return null;
                input.FirePressed = false;
                Assert.That(weapon.Item.MagazineAmmo, Is.EqualTo(ammoBeforeFire - 1));
                pawn.Root.GetComponent<Renderer>().material.color = new Color(1f, 0.3f, 0.12f);
                PositionEvidenceCamera(cameraRoot.transform, cameraRuntime, pawn.Root.transform);
                yield return Capture(evidenceDirectory, "input-fire.png");

                input.ReloadPressed = true;
                yield return null;
                input.ReloadPressed = false;
                Assert.That(weapon.ReloadCount, Is.EqualTo(1));
                pawn.Root.GetComponent<Renderer>().material.color = new Color(0.25f, 1f, 0.45f);
                PositionEvidenceCamera(cameraRoot.transform, cameraRuntime, pawn.Root.transform);
                yield return Capture(evidenceDirectory, "input-reload.png");

                input.MeleePressed = true;
                yield return null;
                input.MeleePressed = false;
                Assert.That(weapon.MeleeCount, Is.EqualTo(1));
                pawn.Root.GetComponent<Renderer>().material.color = new Color(1f, 0.75f, 0.2f);
                PositionEvidenceCamera(cameraRoot.transform, cameraRuntime, pawn.Root.transform);
                yield return Capture(evidenceDirectory, "input-melee.png");

                input.RequestedQuickBarSlot = 1;
                yield return null;
                input.RequestedQuickBarSlot = -1;
                yield return null;
                yield return null;
                Assert.That(pawn.Equipment.CurrentWeapon.Item.Definition, Is.SameAs(sidearmItem));
                pawn.Root.GetComponent<Renderer>().material.color = new Color(0.75f, 0.35f, 1f);
                PositionEvidenceCamera(cameraRoot.transform, cameraRuntime, pawn.Root.transform);
                yield return Capture(evidenceDirectory, "input-switch.png");

                input.Intent = default;
                input.LookDelta = Vector2.zero;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldHost);
                UnityEngine.Object.DestroyImmediate(cameraRoot);
                UnityEngine.Object.DestroyImmediate(lightRoot);
                UnityEngine.Object.DestroyImmediate(ground);
                UnityEngine.Object.Destroy(gameModeDefinition);
                UnityEngine.Object.Destroy(controllerDefinition);
                UnityEngine.Object.Destroy(initialSet);
                UnityEngine.Object.Destroy(sidearmItem);
                UnityEngine.Object.Destroy(itemDefinition);
                UnityEngine.Object.Destroy(sidearmEquipment);
                UnityEngine.Object.Destroy(equipment);
                UnityEngine.Object.Destroy(pawnData);
                UnityEngine.Object.Destroy(prefab);
            }
        }

        [UnityTest]
        public IEnumerator InitialEquipActionsSwitchFailureAndRespawn_RunThroughGameplayLoop()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            prefab.name = "EquipmentPawnPrefab";
            prefab.SetActive(false);
            PawnData pawnData = PawnData.CreateRuntime(prefab, new AbilitySet());
            WeaponEquipmentDefinition rifleEquipment = WeaponEquipmentDefinition.CreateRuntime(10, 1);
            WeaponEquipmentDefinition sidearmEquipment = WeaponEquipmentDefinition.CreateRuntime(6, 1);
            WeaponEquipmentDefinition failedEquipment = WeaponEquipmentDefinition.CreateRuntime(4, 1, true);
            WeaponItemDefinition rifleItem = WeaponItemDefinition.CreateRuntime(rifleEquipment, 2, 8);
            WeaponItemDefinition sidearmItem = WeaponItemDefinition.CreateRuntime(sidearmEquipment, 3, 3);
            WeaponItemDefinition failedItem = WeaponItemDefinition.CreateRuntime(failedEquipment, 2, 2);
            InitialInventorySet initialSet = InitialInventorySet.CreateRuntime(
                0,
                rifleItem,
                sidearmItem,
                failedItem);
            PossessionControllerDefinition controllerDefinition =
                ScriptableObject.CreateInstance<PossessionControllerDefinition>();
            EquipmentPlayModeGameModeDefinition gameModeDefinition =
                ScriptableObject.CreateInstance<EquipmentPlayModeGameModeDefinition>();
            gameModeDefinition.Configure(pawnData, controllerDefinition, initialSet);
            GameObject worldHost = new GameObject("WorldBehaviour.InventoryEquipmentTest");
            WorldBehaviour behaviour = worldHost.AddComponent<WorldBehaviour>();
            GameObject cameraRoot = new GameObject("EquipmentLifecycleCamera");
            Camera camera = cameraRoot.AddComponent<Camera>();
            cameraRoot.transform.position = new Vector3(0f, 1f, -5f);
            cameraRoot.transform.LookAt(Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            GameObject lightRoot = new GameObject("EquipmentLifecycleLight");
            Light light = lightRoot.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightRoot.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            string evidenceDirectory = Environment.GetEnvironmentVariable("CGAME_EQUIPMENT_VISUAL_EVIDENCE_DIR");

            try
            {
                Task<WorldStartResult> startTask = behaviour.StartWorld(
                    Array.Empty<IWorldCoreService>(),
                    new GameLauncher(new Func<ILaunchStep>[] { () => new EquipmentReadyStep() }));
                while (!startTask.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(startTask.Result.Succeeded, Is.True);
                Assert.That(behaviour.RuntimeWorld.StartGame(
                    new GameplaySessionFactory(gameModeDefinition)).Succeeded, Is.True);
                GameMode gameMode = ((GameManager)behaviour.RuntimeWorld.CurrentGameSession).CurrentGameMode;
                PlayerController controller = gameMode.LocalPlayerController;
                var starts = new PlayerStartRegistry();
                starts.Register(new PlayerStartInfo("local", Vector3.zero, Quaternion.identity));
                starts.Register(new PlayerStartInfo("respawn", Vector3.right, Quaternion.identity));
                Assert.That(gameMode.PrepareLocalPawn(new PawnFactory(), starts), Is.True);
                Assert.That(gameMode.CommitLocalPawn(), Is.True);
                Assert.That(behaviour.RuntimeWorld.State, Is.EqualTo(WorldState.StartingGame));
                yield return null;
                yield return null;
                yield return null;

                PawnAssembly firstPawn = gameMode.CurrentPawnAssembly;
                WeaponInstance rifle = firstPawn.Equipment.CurrentWeapon;
                Assert.That(behaviour.RuntimeWorld.State, Is.EqualTo(WorldState.Running));
                Assert.That(rifle, Is.Not.Null);
                Renderer renderer = firstPawn.Root.GetComponent<Renderer>();
                renderer.material.color = new Color(0.15f, 0.65f, 1f);
                yield return Capture(evidenceDirectory, "equipped-rifle.png");

                Assert.That(rifle.Fire(), Is.True);
                renderer.material.color = new Color(1f, 0.3f, 0.12f);
                yield return Capture(evidenceDirectory, "fire.png");
                Assert.That(rifle.Reload(), Is.EqualTo(8));
                renderer.material.color = new Color(0.25f, 1f, 0.45f);
                yield return Capture(evidenceDirectory, "reload.png");
                Assert.That(rifle.Melee(), Is.True);

                Assert.That(controller.QuickBar.SelectSlot(1), Is.True);
                yield return null;
                yield return null;
                yield return null;
                WeaponInstance sidearm = firstPawn.Equipment.CurrentWeapon;
                Assert.That(sidearm.Item.Definition, Is.SameAs(sidearmItem));
                renderer.material.color = new Color(0.75f, 0.35f, 1f);
                yield return Capture(evidenceDirectory, "switched-sidearm.png");

                Assert.That(controller.QuickBar.SelectSlot(2), Is.True);
                yield return null;
                yield return null;
                yield return null;
                Assert.That(firstPawn.Equipment.CurrentWeapon, Is.SameAs(sidearm));
                Assert.That(firstPawn.Equipment.LastFailure, Is.Not.Empty);
                renderer.material.color = new Color(1f, 0.75f, 0.2f);
                yield return Capture(evidenceDirectory, "failed-switch-retained-sidearm.png");

                Assert.That(controller.QuickBar.SelectSlot(0), Is.True);
                yield return null;
                yield return null;
                yield return null;
                ItemInstance persistentRifleItem = firstPawn.Equipment.CurrentWeapon.Item;
                Assert.That(gameMode.PrepareLocalPawn(new PawnFactory(), starts), Is.True);
                Assert.That(gameMode.CommitLocalPawn(), Is.True);
                yield return null;
                yield return null;
                yield return null;
                Assert.That(gameMode.CurrentPawnAssembly.Equipment.CurrentWeapon.Item,
                    Is.SameAs(persistentRifleItem));
                gameMode.CurrentPawnAssembly.Root.GetComponent<Renderer>().material.color =
                    new Color(0.15f, 0.65f, 1f);
                yield return Capture(evidenceDirectory, "respawn-rebuilt-rifle.png");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldHost);
                UnityEngine.Object.DestroyImmediate(cameraRoot);
                UnityEngine.Object.DestroyImmediate(lightRoot);
                UnityEngine.Object.Destroy(gameModeDefinition);
                UnityEngine.Object.Destroy(controllerDefinition);
                UnityEngine.Object.Destroy(initialSet);
                UnityEngine.Object.Destroy(failedItem);
                UnityEngine.Object.Destroy(sidearmItem);
                UnityEngine.Object.Destroy(rifleItem);
                UnityEngine.Object.Destroy(failedEquipment);
                UnityEngine.Object.Destroy(sidearmEquipment);
                UnityEngine.Object.Destroy(rifleEquipment);
                UnityEngine.Object.Destroy(pawnData);
                UnityEngine.Object.Destroy(prefab);
            }
        }

        private static IEnumerator Capture(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                yield break;
            }

            Directory.CreateDirectory(directory);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, fileName));
            yield return null;
        }

        private static void PositionEvidenceCamera(
            Transform cameraTransform,
            IPlayerCameraRuntime cameraRuntime,
            Transform target)
        {
            cameraTransform.position = cameraRuntime.Position + new Vector3(0f, 0.3f, -5f);
            cameraTransform.LookAt(target.position + Vector3.up);
        }

        private sealed class CutoverInputService :
            IWorldCoreService,
            IWorldTickCoreService,
            IPlayerInputSource
        {
            public string Name => "CutoverInput";

            public TickGroup TickGroup => TickGroup.TG_Input;

            public CharacterControlIntent Intent { get; set; }

            public Vector2 LookDelta { get; set; }

            public bool FirePressed { get; set; }

            public bool ReloadPressed { get; set; }

            public bool MeleePressed { get; set; }

            public int RequestedQuickBarSlot { get; set; } = -1;

            public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task ShutdownAsync() => Task.CompletedTask;

            public void Tick(float deltaTime)
            {
            }

            public CharacterControlIntent ReadControlIntent() => Intent;

            public Vector2 ReadLookDelta(float deltaTime) => LookDelta;
        }

        private sealed class EquipmentReadyStep : ILaunchStep
        {
            public string Name => "Ready";

            public Task<LaunchStepResult> ExecuteAsync(
                LaunchContext context,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(LaunchStepResult.Success());
            }

            public Task ExitAsync(LaunchContext context) => Task.CompletedTask;
        }
    }

    public sealed class EquipmentPlayModeGameModeDefinition : GameModeDefinition
    {
        private PawnData pawnData;
        private ControllerDefinition controllerDefinition;
        private InitialInventorySet initialInventorySet;

        public void Configure(
            PawnData pawnData,
            ControllerDefinition controllerDefinition,
            InitialInventorySet initialInventorySet)
        {
            this.pawnData = pawnData;
            this.controllerDefinition = controllerDefinition;
            this.initialInventorySet = initialInventorySet;
        }

        public override PawnData ResolvePawnData(GameStartRequest request) => pawnData;

        public override InitialInventorySet ResolveInitialInventorySet(GameStartRequest request)
        {
            return initialInventorySet;
        }

        public override GameMode CreateRuntime(GameModeCreationContext context, PawnData resolvedPawnData)
        {
            return new GameMode(context, controllerDefinition, resolvedPawnData);
        }
    }
}
