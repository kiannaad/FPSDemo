using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class CharacterSpawnPipelinePlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private GameObject ground;
        private GameObject lifetimeCamera;
        private object cameraTargetBindingCoordinator;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "SpawnPipelineGround";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
        }

        [TearDown]
        public override void TearDown()
        {
            if (cameraTargetBindingCoordinator != null)
            {
                Invoke(cameraTargetBindingCoordinator, "Dispose");
                cameraTargetBindingCoordinator = null;
            }

            if (lifetimeCamera != null)
            {
                UnityEngine.Object.DestroyImmediate(lifetimeCamera);
                lifetimeCamera = null;
            }

            DestroyIfPresent("[GameManager]");
            DestroyIfPresent("[CharacterRuntimeRoot]");
            if (ground != null)
            {
                UnityEngine.Object.DestroyImmediate(ground);
                ground = null;
            }

            keyboard = null;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator CameraTargetBinding_UnbindsBeforeReleaseAndRebindsAfterRespawn()
        {
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithInMemoryDefinition();
            Type bindingType = RequireRuntimeType("CGame.FirstPersonCameraBinding");
            Type coordinatorType = RequireRuntimeType("CGame.LocalPlayerCameraTargetBinding");
            object binding = Activator.CreateInstance(bindingType);
            cameraTargetBindingCoordinator = Activator.CreateInstance(coordinatorType, spawnManager, binding);
            lifetimeCamera = new GameObject("LocalPlayerCameraRig");
            lifetimeCamera.AddComponent<Camera>();

            object firstOperation = Invoke(spawnManager, "BeginSpawn", CreateRequest("camera-binding-first", "FirstCameraTarget", Vector3.zero));
            for (int i = 0; i < 6; i++)
            {
                Invoke(spawnManager, "Update", 0f);
            }

            Assert.AreEqual("CharacterReady", GetProperty<object>(firstOperation, "State").ToString());
            Assert.IsTrue(GetProperty<bool>(binding, "IsBound"));
            object firstTarget = GetProperty<object>(binding, "Target");
            Assert.AreEqual("FirstPersonCameraAnchor", firstTarget.GetType().Name);
            Component firstAnchor = (Component)firstTarget;
            Transform firstVisual = GameObject.Find("FirstCameraTarget/CharacterVisual").transform;
            Assert.AreEqual("FirstPersonCameraAnchor", firstAnchor.transform.name);
            Assert.IsTrue(firstAnchor.transform.IsChildOf(firstVisual));
            Assert.IsTrue(
                GetProperty<bool>(firstTarget, "UsesAnimatedHeadMount"),
                "The spawned camera target must use the animated Head mount.");
            Transform firstHead = (Transform)firstTarget.GetType()
                .GetField("animatedHead", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(firstTarget);
            Assert.NotNull(firstHead);
            Assert.That(
                Vector3.Distance(GetProperty<Vector3>(firstTarget, "Position"), firstHead.position),
                Is.LessThan(0.15f),
                "The animated camera mount must stay local to the owner Head instead of using a fixed world height.");
            Assert.AreEqual("Bound", GetProperty<object>(cameraTargetBindingCoordinator, "LastResult").ToString());

            bool releasingObserved = false;
            bool bindingUnboundDuringReleasing = false;
            bool characterExistsDuringReleasing = false;
            AddEventHandler(spawnManager, "CharacterReleasing", arguments =>
            {
                releasingObserved = true;
                bindingUnboundDuringReleasing = !GetProperty<bool>(binding, "IsBound");
                characterExistsDuringReleasing = GameObject.Find("FirstCameraTarget") != null;
            });

            object reason = Enum.Parse(RequireRuntimeType("CGame.CharacterDespawnReason"), "Requested");
            Assert.IsTrue((bool)Invoke(spawnManager, "Despawn", GetProperty<object>(firstOperation, "RuntimeId"), reason));
            Invoke(spawnManager, "Update", 0f);
            yield return null;

            Assert.IsTrue(releasingObserved);
            Assert.IsTrue(bindingUnboundDuringReleasing);
            Assert.IsTrue(characterExistsDuringReleasing);
            Assert.IsFalse(GetProperty<bool>(binding, "IsBound"));
            Assert.NotNull(lifetimeCamera);
            Assert.IsNull(GameObject.Find("FirstCameraTarget"));

            object secondOperation = Invoke(spawnManager, "BeginSpawn", CreateRequest("camera-binding-second", "SecondCameraTarget", new Vector3(2f, 0f, 0f)));
            for (int i = 0; i < 6; i++)
            {
                Invoke(spawnManager, "Update", 0f);
            }

            Assert.AreEqual("CharacterReady", GetProperty<object>(secondOperation, "State").ToString());
            Assert.IsTrue(GetProperty<bool>(binding, "IsBound"));
            Assert.AreEqual("FirstPersonCameraAnchor", GetProperty<object>(binding, "Target").GetType().Name);
            Assert.AreNotSame(firstTarget, GetProperty<object>(binding, "Target"));
        }

        [UnityTest]
        public IEnumerator CameraTargetBinding_MissingAnchorReturnsExplicitFailure()
        {
            GameObject characterWithoutAnchor = new GameObject("CharacterWithoutCameraAnchor");
            Type bindingType = RequireRuntimeType("CGame.FirstPersonCameraBinding");
            object binding = Activator.CreateInstance(bindingType);

            object result = Invoke(binding, "BindCharacter", characterWithoutAnchor.transform);

            Assert.AreEqual("MissingAnchor", result.ToString());
            Assert.IsFalse(GetProperty<bool>(binding, "IsBound"));
            UnityEngine.Object.Destroy(characterWithoutAnchor);
            yield return null;
            Assert.IsTrue(characterWithoutAnchor == null);
        }

        [UnityTest]
        public IEnumerator CharacterReady_RegistersPhysicsAndMovesFromNextFullFrame()
        {
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithInMemoryDefinition();
            Assert.NotNull(spawnManager);

            object operation = Invoke(spawnManager, "BeginSpawn", CreateRequest("playmode-ready", "SpawnPipelineCharacter", Vector3.zero));
            for (int i = 0; i < 6; i++)
            {
                Invoke(spawnManager, "Update", 0f);
            }

            Assert.AreEqual("CharacterReady", GetProperty<object>(operation, "State").ToString());
            GameObject character = GameObject.Find("SpawnPipelineCharacter");
            Assert.NotNull(character);
            Component motor = character.GetComponent(RequireRuntimeType("CGame.CharacterPhysicsMotor"));
            object registration = motor.GetType().GetField("physicsRegistration", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(motor);
            Assert.NotNull(registration);
            Assert.IsTrue(GetProperty<bool>(registration, "IsActive"));
            Type controllerManagerType =
                RequireRuntimeType("CGame.ControllerManager");
            object controllerManager = RequireRuntimeType("CGame.GameManager")
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                    method.Name == "GetManager"
                    && method.IsGenericMethodDefinition)
                .MakeGenericMethod(controllerManagerType)
                .Invoke(null, null);
            object playerController = controllerManagerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(method =>
                    method.Name == "GettingController"
                    && method.IsGenericMethodDefinition)
                .MakeGenericMethod(
                    RequireRuntimeType("CGame.PlayerController"))
                .Invoke(controllerManager, null);
            Assert.NotNull(playerController);
            object weaponRuntime =
                GetProperty<object>(playerController, "WeaponRuntime");
            Assert.IsTrue(GetProperty<bool>(weaponRuntime, "IsInitialized"));
            object snapshot =
                GetProperty<object>(weaponRuntime, "Snapshot");
            object equippedWeaponId =
                GetProperty<object>(snapshot, "EquippedWeaponId");
            Assert.AreEqual(
                "knife",
                GetProperty<string>(equippedWeaponId, "Value"));
            object activeAction =
                GetProperty<object>(weaponRuntime, "ActiveAction");
            Assert.IsFalse(GetProperty<bool>(activeAction, "IsValid"));

            Animator animator = character.GetComponentInChildren<Animator>();
            PlayableGraph graph = animator.playableGraph;
            Assert.AreEqual(2, graph.GetOutputCount());
            var master = (AnimationLayerMixerPlayable)graph
                .GetOutput(1)
                .GetSourcePlayable();
            Assert.AreEqual(1f, master.GetInputWeight(1));
            var overrideMixer = (AnimationLayerMixerPlayable)master.GetInput(1);
            var slotMixer = (AnimationLayerMixerPlayable)overrideMixer.GetInput(0);
            var overlayMixer = (AnimationLayerMixerPlayable)slotMixer.GetInput(0);
            Assert.AreEqual(1f, overlayMixer.GetInputWeight(0));
            Assert.AreEqual(0, CountConnectedInputs(slotMixer, 1));

            Vector3 positionAtReady = character.transform.position;
            Press(keyboard.wKey);
            yield return null;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            Release(keyboard.wKey);
            yield return null;

            Assert.Greater(character.transform.position.z, positionAtReady.z + 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DefaultKnife_ReadyFirstFrameCapturesFullWeightOverlayWithoutEquip()
        {
            object spawnManager =
                CharacterSpawnTestConfiguration.CreateManagerWithInMemoryDefinition();
            object operation = Invoke(
                spawnManager,
                "BeginSpawn",
                CreateRequest(
                    "default-knife-evidence",
                    "DefaultKnifeEvidenceCharacter",
                    Vector3.zero));
            for (int i = 0; i < 6; i++)
            {
                Invoke(spawnManager, "Update", 0f);
            }

            Assert.AreEqual(
                "CharacterReady",
                GetProperty<object>(operation, "State").ToString());
            GameObject character =
                GameObject.Find("DefaultKnifeEvidenceCharacter");
            Assert.NotNull(character);
            Animator animator = character.GetComponentInChildren<Animator>();
            PlayableGraph graph = animator.playableGraph;
            var master = (AnimationLayerMixerPlayable)graph
                .GetOutput(1)
                .GetSourcePlayable();
            var overrideMixer =
                (AnimationLayerMixerPlayable)master.GetInput(1);
            var slotMixer =
                (AnimationLayerMixerPlayable)overrideMixer.GetInput(0);
            var overlayMixer =
                (AnimationLayerMixerPlayable)slotMixer.GetInput(0);
            Assert.AreEqual(1f, master.GetInputWeight(1));
            Assert.AreEqual(1f, overlayMixer.GetInputWeight(0));
            Assert.AreEqual(0, CountConnectedInputs(slotMixer, 1));

            var cameraObject =
                new GameObject("Default Knife Evidence Camera");
            var lightObject =
                new GameObject("Default Knife Evidence Light");
            Camera camera = cameraObject.AddComponent<Camera>();
            Light light = lightObject.AddComponent<Light>();
            var target =
                new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
            Camera[] sceneCameras = UnityEngine.Object
                .FindObjectsOfType<Camera>();
            bool[] cameraStates =
                sceneCameras.Select(item => item.enabled).ToArray();
            string evidenceDirectory = Path.Combine(
                Application.temporaryCachePath,
                "WeaponResourcesAndInitialWeaponEvidence");
            Directory.CreateDirectory(evidenceDirectory);
            string evidencePath = Path.Combine(
                evidenceDirectory,
                "01-default-knife-ready-first-frame.png");

            try
            {
                foreach (Camera sceneCamera in sceneCameras)
                {
                    if (sceneCamera != camera)
                    {
                        sceneCamera.enabled = false;
                    }
                }

                camera.transform.position =
                    new Vector3(2.6f, 1.45f, 3.2f);
                camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
                camera.fieldOfView = 34f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor =
                    new Color(0.12f, 0.14f, 0.18f);
                camera.targetTexture = target;
                target.Create();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation =
                    Quaternion.Euler(35f, -30f, 0f);

                yield return new WaitForEndOfFrame();
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                var texture = new Texture2D(
                    target.width,
                    target.height,
                    TextureFormat.RGB24,
                    false);
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(
                        new Rect(0f, 0f, target.width, target.height),
                        0,
                        0);
                    texture.Apply();
                    File.WriteAllBytes(
                        evidencePath,
                        texture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.Destroy(texture);
                }

                Assert.IsTrue(File.Exists(evidencePath));
                TestContext.Out.WriteLine(evidencePath);
            }
            finally
            {
                for (int i = 0; i < sceneCameras.Length; i++)
                {
                    if (sceneCameras[i] != null
                        && sceneCameras[i] != camera)
                    {
                        sceneCameras[i].enabled = cameraStates[i];
                    }
                }

                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraObject);
                UnityEngine.Object.Destroy(lightObject);
            }
        }

        [UnityTest]
        public IEnumerator Despawn_ReleasesOneRuntimeWithoutInvalidatingAnother()
        {
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithInMemoryDefinition();
            object firstOperation = Invoke(spawnManager, "BeginSpawn", CreateRequest("first-runtime", "FirstRuntimeCharacter", Vector3.zero));
            object secondOperation = Invoke(spawnManager, "BeginSpawn", CreateRequest("second-runtime", "SecondRuntimeCharacter", new Vector3(2f, 0f, 0f)));
            for (int i = 0; i < 6; i++)
            {
                Invoke(spawnManager, "Update", 0f);
            }

            object firstRuntimeId = GetProperty<object>(firstOperation, "RuntimeId");
            object secondRuntimeId = GetProperty<object>(secondOperation, "RuntimeId");
            object reason = Enum.Parse(RequireRuntimeType("CGame.CharacterDespawnReason"), "Requested");
            Assert.IsTrue((bool)Invoke(spawnManager, "Despawn", firstRuntimeId, reason));
            Assert.IsFalse((bool)Invoke(spawnManager, "Despawn", firstRuntimeId, reason));
            Invoke(spawnManager, "Update", 0f);
            yield return null;

            object[] firstLookup = { firstRuntimeId, null };
            object[] secondLookup = { secondRuntimeId, null };
            Assert.IsFalse((bool)InvokeWithArguments(spawnManager, "TryGetCharacterView", firstLookup));
            Assert.IsTrue((bool)InvokeWithArguments(spawnManager, "TryGetCharacterView", secondLookup));
            Assert.IsNull(GameObject.Find("FirstRuntimeCharacter"));
            Assert.NotNull(GameObject.Find("SecondRuntimeCharacter"));
            Component secondMotor = GameObject.Find("SecondRuntimeCharacter").GetComponent(RequireRuntimeType("CGame.CharacterPhysicsMotor"));
            object secondRegistration = secondMotor.GetType().GetField("physicsRegistration", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(secondMotor);
            Assert.NotNull(secondRegistration);
            Assert.IsTrue(GetProperty<bool>(secondRegistration, "IsActive"));
        }

        [UnityTest]
        public IEnumerator GameManagerShutdown_ReleasesRuntimeBeforePhysicsDependency()
        {
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithInMemoryDefinition();
            object operation = Invoke(spawnManager, "BeginSpawn", CreateRequest("shutdown-runtime", "ShutdownRuntimeCharacter", Vector3.zero));
            for (int i = 0; i < 6; i++)
            {
                Invoke(spawnManager, "Update", 0f);
            }

            Assert.AreEqual("CharacterReady", GetProperty<object>(operation, "State").ToString());
            bool releaseObserved = false;
            bool physicsAliveDuringRelease = false;
            AddEventHandler(spawnManager, "CharacterReleased", arguments =>
            {
                releaseObserved = true;
                physicsAliveDuringRelease = RequireRuntimeType("CGame.PhysicsManager")
                    .GetProperty("CurrentWorld", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null) != null;
            });

            GameObject managerObject = GameObject.Find("[GameManager]");
            Assert.NotNull(managerObject);
            UnityEngine.Object.DestroyImmediate(managerObject);
            yield return null;

            Assert.IsTrue(releaseObserved);
            Assert.IsTrue(physicsAliveDuringRelease);
            Assert.IsNull(RequireRuntimeType("CGame.PhysicsManager").GetProperty("CurrentWorld", BindingFlags.Public | BindingFlags.Static)?.GetValue(null));
            Assert.IsNull(GameObject.Find("ShutdownRuntimeCharacter"));
        }

        private static object CreateRequest(string requestIdValue, string displayName, Vector3 position)
        {
            CharacterDefinition definition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            Type requestType = RequireRuntimeType("CGame.CharacterSpawnRequest");
            object requestId = Activator.CreateInstance(RequireRuntimeType("CGame.CharacterSpawnRequestId"), requestIdValue);
            object placement = Activator.CreateInstance(RequireRuntimeType("CGame.CharacterSpawnPlacement"), position, Quaternion.identity);
            return Activator.CreateInstance(requestType, requestId, definition.DefinitionId, CharacterControlKind.LocalPlayer, placement, InputType.Player, displayName);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            return target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }

        private static object InvokeWithArguments(object target, string methodName, object[] arguments)
        {
            return target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }

        private static int CountConnectedInputs(Playable playable, int firstInput)
        {
            int connected = 0;
            for (int i = firstInput; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInput(i).IsValid())
                {
                    connected++;
                }
            }

            return connected;
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static object GetStaticInstance(Type type)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (property != null)
                {
                    return property.GetValue(null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static void AddEventHandler(object target, string eventName, Action<object[]> callback)
        {
            EventInfo eventInfo = target.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            ParameterInfo[] eventParameters = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();
            ParameterExpression[] parameters = eventParameters
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();
            NewArrayExpression arguments = Expression.NewArrayInit(typeof(object), parameters.Select(parameter => Expression.Convert(parameter, typeof(object))));
            MethodCallExpression body = Expression.Call(Expression.Constant(callback), typeof(Action<object[]>).GetMethod("Invoke"), arguments);
            Delegate handler = Expression.Lambda(eventInfo.EventHandlerType, body, parameters).Compile();
            eventInfo.AddEventHandler(target, handler);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Runtime type was not found: {fullName}");
            return type;
        }

        private static void DestroyIfPresent(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }

    internal static class CharacterSpawnTestConfiguration
    {
        public static object CreateManagerWithInMemoryDefinition(
            params WeaponAnimationDefinition[] weaponDefinitions)
        {
            Type gameManagerType = RequireRuntimeType("CGame.GameManager");
            _ = GetStaticInstance(gameManagerType);
            object spawnManager = gameManagerType.GetMethod("CreateManager", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { RequireRuntimeType("CGame.CharacterSpawnManager") });
            if (spawnManager == null)
            {
                throw new InvalidOperationException("CharacterSpawnManager could not be created.");
            }

            CharacterDefinition definition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            spawnManager.GetType().GetField("definitionProvider", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(spawnManager, new InMemoryCharacterDefinitionProvider(new[] { definition }));
            if (weaponDefinitions == null
                || weaponDefinitions.Length == 0)
            {
                WeaponAnimationDefinition knife =
                    Resources.Load<WeaponAnimationDefinition>(
                        "FistsWeaponAnimationDefinition");
                WeaponAnimationDefinition rifle =
                    LoadEditorAsset<WeaponAnimationDefinition>(
                        "Assets/Art/Animation/Weapon/KINEMATION/AK/"
                        + "RifleAKAnimationDefinition.asset");
                weaponDefinitions = rifle == null
                    ? new[] { knife }
                    : new[] { knife, rifle };
            }

            spawnManager.GetType()
                .GetField(
                    "weaponDefinitionProvider",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    spawnManager,
                    new InMemoryWeaponAnimationDefinitionProvider(
                        weaponDefinitions));
            return spawnManager;
        }

        private static T LoadEditorAsset<T>(string path)
            where T : UnityEngine.Object
        {
            Type assetDatabaseType =
                Type.GetType(
                    "UnityEditor.AssetDatabase, UnityEditor.CoreModule");
            MethodInfo loadMethod = assetDatabaseType?.GetMethod(
                "LoadAssetAtPath",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(Type) },
                null);
            return loadMethod?.Invoke(
                       null,
                       new object[] { path, typeof(T) })
                   as T;
        }

        private static object GetStaticInstance(Type type)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (property != null)
                {
                    return property.GetValue(null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp")
                ?? Type.GetType($"{fullName}, CGame.Input");
            return type ?? throw new InvalidOperationException($"Runtime type not found: {fullName}");
        }
    }
}
