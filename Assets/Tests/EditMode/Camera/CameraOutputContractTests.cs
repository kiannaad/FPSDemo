using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class CameraOutputContractTests
    {
        [Test]
        public void CameraManager_RunsAfterPhysicsPresentation()
        {
            object cameraManager = Activator.CreateInstance(RequireRuntimeType("CGame.CameraManager"));
            object physicsManager = Activator.CreateInstance(RequireRuntimeType("CGame.PhysicsManager"));

            int cameraPriority = GetProperty<int>(cameraManager, "Priority");
            int physicsPriority = GetProperty<int>(physicsManager, "Priority");

            Assert.AreEqual(60, cameraPriority);
            Assert.Greater(physicsPriority, cameraPriority,
                "GameManager iterates high priorities first, so Camera must sample after Physics.Present.");
        }

        [Test]
        public void CameraDebugSnapshot_IsAnImmutableValue()
        {
            Type snapshotType = RequireRuntimeType("CGame.CameraDebugSnapshot");

            Assert.IsTrue(snapshotType.IsValueType);
            Assert.IsTrue(snapshotType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .All(field => field.IsInitOnly));
            Assert.IsTrue(snapshotType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .All(property => property.SetMethod == null));
        }

        [Test]
        public void LocalOwnerWorldBodyVisibility_IsCameraSpecificAndRestoresState()
        {
            const int ownerWorldBodyLayer = 14;
            Type visibilityType = RequireRuntimeType("CGame.LocalOwnerWorldBodyVisibility");
            var worldCameraObject = new GameObject("WorldCamera");
            var observerCameraObject = new GameObject("ObserverCamera");
            var character = new GameObject("LocalOwnerCharacter");
            var rendererObject = new GameObject("WorldBodyRenderer");
            var colliderObject = new GameObject("GameplayCollider");
            Camera worldCamera = worldCameraObject.AddComponent<Camera>();
            Camera observerCamera = observerCameraObject.AddComponent<Camera>();
            Animator animator = character.AddComponent<Animator>();
            rendererObject.transform.SetParent(character.transform);
            colliderObject.transform.SetParent(character.transform);
            rendererObject.layer = 12;
            colliderObject.layer = 11;
            Renderer renderer = rendererObject.AddComponent<MeshRenderer>();
            Collider collider = colliderObject.AddComponent<BoxCollider>();
            worldCamera.cullingMask = -1;
            observerCamera.cullingMask = -1;
            object visibility = null;

            try
            {
                visibility = Activator.CreateInstance(visibilityType, worldCamera, ownerWorldBodyLayer);
                Assert.IsTrue((bool)Invoke(visibility, "BindCharacter", character.transform));

                int ownerLayerMask = 1 << ownerWorldBodyLayer;
                Assert.AreEqual(ownerWorldBodyLayer, rendererObject.layer);
                Assert.AreEqual(11, colliderObject.layer, "Gameplay collider layers must not be repurposed for rendering.");
                Assert.AreEqual(0, worldCamera.cullingMask & ownerLayerMask);
                Assert.AreNotEqual(0, observerCamera.cullingMask & ownerLayerMask);
                Assert.IsTrue(renderer.enabled);
                Assert.IsTrue(animator.enabled);
                Assert.IsTrue(collider.enabled);
                Assert.IsTrue(character.activeSelf);
                Assert.AreEqual(1, GetProperty<int>(visibility, "AffectedRendererCount"));

                Invoke(visibility, "Unbind");
                Assert.AreEqual(12, rendererObject.layer);
                Assert.AreEqual(0, worldCamera.cullingMask & ownerLayerMask,
                    "The world Camera keeps the owner-only layer excluded between respawns.");

                Assert.IsTrue((bool)Invoke(visibility, "BindCharacter", character.transform));
                Invoke(visibility, "Dispose");
                Assert.AreEqual(12, rendererObject.layer);
                Assert.AreEqual(-1, worldCamera.cullingMask);
            }
            finally
            {
                if (visibility != null)
                {
                    Invoke(visibility, "Dispose");
                }

                UnityEngine.Object.DestroyImmediate(character);
                UnityEngine.Object.DestroyImmediate(observerCameraObject);
                UnityEngine.Object.DestroyImmediate(worldCameraObject);
            }
        }

        [Test]
        public void CinemachineOutput_CreatesSingleUrpCameraWithoutViewModelStack()
        {
            Type outputType = RequireRuntimeType("CGame.CinemachineCameraOutput");
            object output = null;
            int cameraCountBefore = UnityEngine.Object.FindObjectsOfType<Camera>().Length;
            int listenerCountBefore = UnityEngine.Object.FindObjectsOfType<AudioListener>().Length;

            try
            {
                output = InvokeStatic(outputType, "Create");
                Camera worldCamera = GetProperty<Camera>(output, "WorldCamera");
                Camera viewModelCamera = GetProperty<Camera>(output, "ViewModelCamera");
                Type additionalCameraDataType = RequireRuntimeType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                Type brainType = RequireRuntimeType("Unity.Cinemachine.CinemachineBrain");
                Component worldData = worldCamera.GetComponent(additionalCameraDataType);
                IList cameraStack = GetProperty<IList>(worldData, "cameraStack");

                Assert.NotNull(worldData);
                Assert.AreEqual("Base", GetProperty<object>(worldData, "renderType").ToString());
                Assert.AreEqual(0, cameraStack.Count);
                Assert.IsTrue(GetProperty<bool>(worldData, "renderPostProcessing"));
                Assert.IsTrue(GetProperty<bool>(worldData, "renderShadows"));
                Assert.NotNull(worldCamera.GetComponent(brainType));
                Assert.IsNull(viewModelCamera);
                Assert.IsNull(GameObject.Find("ViewModel Overlay Camera"));
                Assert.AreEqual(cameraCountBefore + 1, UnityEngine.Object.FindObjectsOfType<Camera>().Length);
                Assert.AreEqual(listenerCountBefore + 1, UnityEngine.Object.FindObjectsOfType<AudioListener>().Length);
            }
            finally
            {
                if (output != null)
                {
                    Invoke(output, "Dispose");
                }
            }
        }

        [Test]
        public void ViewModelPrototype_ContainsOnlyPresentationRenderersOnDedicatedLayer()
        {
            int viewModelLayer = LayerMask.NameToLayer("FirstPersonViewModel");
            Type prototypeType = RequireRuntimeType("CGame.FirstPersonViewModelPrototype");
            var parent = new GameObject("ViewModelParent");
            object prototype = null;

            try
            {
                prototype = InvokeStatic(prototypeType, "Create", parent.transform, viewModelLayer);
                GameObject root = GetProperty<GameObject>(prototype, "Root");
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

                Assert.GreaterOrEqual(viewModelLayer, 0);
                Assert.GreaterOrEqual(renderers.Length, 3, "The prototype must visibly communicate two arms and a weapon silhouette.");
                Assert.IsTrue(renderers.All(renderer => renderer.gameObject.layer == viewModelLayer));
                Assert.AreEqual(0, root.GetComponentsInChildren<Collider>(true).Length);
                Assert.AreEqual(0, root.GetComponentsInChildren<Animator>(true).Length);
            }
            finally
            {
                if (prototype != null)
                {
                    Invoke(prototype, "Dispose");
                }

                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method, $"Method was not found: {methodName}");
            return method.Invoke(target, arguments);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method, $"Static method was not found: {methodName}");
            return method.Invoke(null, arguments);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property, $"Property was not found: {propertyName}");
            return (T)property.GetValue(target);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Runtime type was not found: {fullName}");
            return type;
        }
    }
}
