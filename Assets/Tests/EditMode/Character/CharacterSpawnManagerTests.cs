using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class CharacterSpawnManagerTests
    {
        [TearDown]
        public void TearDown()
        {
            ShutdownManagers();

            GameObject runtimeRoot = GameObject.Find("[CharacterRuntimeRoot]");
            if (runtimeRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }
        }

        [Test]
        public void SpawnIdentifiers_KeepRequestAndRuntimeIdentitySeparate()
        {
            Type requestIdType = RequireRuntimeType("CGame.CharacterSpawnRequestId");
            Type runtimeIdType = RequireRuntimeType("CGame.CharacterRuntimeId");
            object requestA = Activator.CreateInstance(requestIdType, "request-a");
            object requestACopy = Activator.CreateInstance(requestIdType, "request-a");
            object runtimeA = Activator.CreateInstance(runtimeIdType, "runtime-a");

            Assert.IsTrue(GetProperty<bool>(requestA, "IsValid"));
            Assert.AreEqual(requestA, requestACopy);
            Assert.IsTrue(GetProperty<bool>(runtimeA, "IsValid"));
            Assert.AreNotEqual(GetProperty<string>(requestA, "Value"), GetProperty<string>(runtimeA, "Value"));
            Assert.IsFalse(GetProperty<bool>(Activator.CreateInstance(requestIdType), "IsValid"));
            Assert.IsFalse(GetProperty<bool>(Activator.CreateInstance(runtimeIdType), "IsValid"));
        }

        [Test]
        public void SpawnPlacement_RejectsNonFiniteTransforms()
        {
            Assert.IsTrue(GetProperty<bool>(CreatePlacement(Vector3.zero, Quaternion.identity), "IsValid"));
            Assert.IsFalse(GetProperty<bool>(CreatePlacement(new Vector3(float.NaN, 0f, 0f), Quaternion.identity), "IsValid"));
            Assert.IsFalse(GetProperty<bool>(CreatePlacement(Vector3.zero, new Quaternion(0f, 0f, 0f, float.PositiveInfinity)), "IsValid"));
        }

        [Test]
        public void BeginSpawn_DoesNotAssembleUntilManagerUpdate()
        {
            object manager = CreateManager();
            object operation = Invoke(manager, "BeginSpawn", CreateRequest("deferred-request", Vector3.zero));
            GameObject runtimeRoot = GameObject.Find("[CharacterRuntimeRoot]");

            Assert.AreEqual(60, GetProperty<int>(manager, "Priority"));
            Assert.AreEqual("Requested", GetProperty<object>(operation, "State").ToString());
            Assert.NotNull(runtimeRoot);
            Assert.AreEqual(0, runtimeRoot.transform.childCount);

            Invoke(manager, "Update", 0f);

            Assert.AreEqual("ResolvingDefinition", GetProperty<object>(operation, "State").ToString());
            Assert.AreEqual(0, runtimeRoot.transform.childCount);
        }

        [Test]
        public void Update_CommitsLocalPlayerAndPublishesReadyAfterPhysicsRegistration()
        {
            object manager = CreateManager();
            object operation = Invoke(manager, "BeginSpawn", CreateRequest("ready-request", Vector3.zero));

            for (int i = 0; i < 6; i++)
            {
                Invoke(manager, "Update", 0f);
            }

            Assert.AreEqual("CharacterReady", GetProperty<object>(operation, "State").ToString());
            Assert.IsTrue(GetProperty<bool>(GetProperty<object>(operation, "RuntimeId"), "IsValid"));
            Assert.AreEqual("None", GetProperty<object>(operation, "Error").ToString());

            GameObject characterRoot = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(characterRoot, "The ready character root was not active in the scene.");
            Component motor = characterRoot.GetComponent(RequireRuntimeType("CGame.CharacterPhysicsMotor"));
            Assert.NotNull(motor, "The ready character root did not contain its physics motor.");
            Assert.IsTrue(characterRoot.activeInHierarchy);
            PropertyInfo currentWorldProperty = RequireRuntimeType("CGame.PhysicsManager")
                .GetProperty("CurrentWorld", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(currentWorldProperty?.GetValue(null));
        }

        [Test]
        public void Update_RejectsInvalidPlacementBeforeAssembly()
        {
            object manager = CreateManager();
            object operation = Invoke(manager, "BeginSpawn", CreateRequest("invalid-placement", new Vector3(float.NaN, 0f, 0f)));

            Invoke(manager, "Update", 0f);
            Invoke(manager, "Update", 0f);

            Assert.AreEqual("Failed", GetProperty<object>(operation, "State").ToString());
            Assert.AreEqual("InvalidPlacement", GetProperty<object>(operation, "Error").ToString());
            Assert.AreEqual(0, GameObject.Find("[CharacterRuntimeRoot]").transform.childCount);
        }

        [Test]
        public void RegistrationHandles_AreStableAndIdempotent()
        {
            object pawnManager = Activator.CreateInstance(RequireRuntimeType("CGame.PawnManager"));
            object pawn = Activator.CreateInstance(RequireRuntimeType("CGame.Pawn"));
            object pawnRegistration = Invoke(pawnManager, "RegisterPawn", pawn);
            object duplicatePawnRegistration = Invoke(pawnManager, "RegisterPawn", pawn);
            Assert.AreSame(pawnRegistration, duplicatePawnRegistration);
            Assert.IsTrue(GetProperty<bool>(pawnRegistration, "IsActive"));
            Invoke(pawnRegistration, "Dispose");
            Invoke(pawnRegistration, "Dispose");
            Assert.IsFalse(GetProperty<bool>(pawnRegistration, "IsActive"));

            object controllerManager = Activator.CreateInstance(RequireRuntimeType("CGame.ControllerManager"));
            object controller = Activator.CreateInstance(RequireRuntimeType("CGame.PlayerController"));
            object controllerRegistration = Invoke(controllerManager, "RegisterController", controller);
            object duplicateControllerRegistration = Invoke(controllerManager, "RegisterController", controller);
            Assert.AreSame(controllerRegistration, duplicateControllerRegistration);
            Assert.IsTrue(GetProperty<bool>(controllerRegistration, "IsActive"));
            Invoke(controllerRegistration, "Dispose");
            Invoke(controllerRegistration, "Dispose");
            Assert.IsFalse(GetProperty<bool>(controllerRegistration, "IsActive"));
        }

        [Test]
        public void ReadyEvent_ExposesViewAndDefersDespawnUntilNextUpdate()
        {
            object manager = CreateManager();
            object operation = Invoke(manager, "BeginSpawn", CreateRequest("lifecycle-events", Vector3.zero));
            object capturedView = null;
            bool readyObserved = false;
            bool releasedObserved = false;
            bool releasedLookupFailed = false;

            AddEventHandler(manager, "CharacterReady", arguments =>
            {
                readyObserved = true;
                object runtimeId = arguments[0];
                object[] lookupArguments = { runtimeId, null };
                Assert.IsTrue((bool)InvokeWithArguments(manager, "TryGetCharacterView", lookupArguments));
                capturedView = lookupArguments[1];
                Assert.IsTrue(GetProperty<bool>(capturedView, "IsValid"));
                Assert.IsTrue((bool)Invoke(manager, "Despawn", runtimeId, Enum.Parse(RequireRuntimeType("CGame.CharacterDespawnReason"), "Requested")));
            });
            AddEventHandler(manager, "CharacterReleased", arguments =>
            {
                releasedObserved = true;
                object[] lookupArguments = { arguments[0], null };
                releasedLookupFailed = !(bool)InvokeWithArguments(manager, "TryGetCharacterView", lookupArguments);
            });

            for (int i = 0; i < 6; i++)
            {
                Invoke(manager, "Update", 0f);
            }

            Assert.IsTrue(readyObserved);
            Assert.IsFalse(releasedObserved);
            Assert.NotNull(GameObject.Find("RuntimeCharacter"));
            object result = GetProperty<object>(operation, "Result");
            Assert.AreEqual(1, result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Length);
            Assert.AreEqual("RuntimeId", result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)[0].Name);

            Invoke(manager, "Update", 0f);

            Assert.IsTrue(releasedObserved);
            Assert.IsTrue(releasedLookupFailed);
            Assert.IsFalse(GetProperty<bool>(capturedView, "IsValid"));
            Assert.AreEqual("Released", GetProperty<object>(capturedView, "State").ToString());
            Assert.IsNull(GetProperty<Transform>(capturedView, "Transform"));
            Assert.IsNull(GameObject.Find("RuntimeCharacter"));
        }

        [Test]
        public void CancelSpawn_WaitsForLateLeaseThenRejectsRequestIdReuse()
        {
            object manager = CreateManager();
            CharacterDefinition definition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            var provider = new DelayedDefinitionProvider();
            SetField(manager, "definitionProvider", provider);
            object request = CreateRequest("cancel-late-result", Vector3.zero);
            object operation = Invoke(manager, "BeginSpawn", request);

            Invoke(manager, "Update", 0f);
            Assert.AreEqual("ResolvingDefinition", GetProperty<object>(operation, "State").ToString());
            Assert.AreSame(operation, Invoke(manager, "BeginSpawn", request));
            object requestId = GetProperty<object>(request, "RequestId");
            Assert.IsTrue((bool)Invoke(manager, "CancelSpawn", requestId));
            Assert.IsTrue((bool)Invoke(manager, "CancelSpawn", requestId));
            Assert.AreEqual("CancelRequested", GetProperty<object>(operation, "State").ToString());

            Invoke(manager, "Update", 0f);
            Assert.AreEqual("CancelRequested", GetProperty<object>(operation, "State").ToString());
            provider.Complete(definition);
            Assert.AreEqual(0, provider.ReleaseCount);
            Invoke(manager, "Update", 0f);

            Assert.AreEqual("Cancelled", GetProperty<object>(operation, "State").ToString());
            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.AreEqual(0, GameObject.Find("[CharacterRuntimeRoot]").transform.childCount);
            object duplicate = Invoke(manager, "BeginSpawn", request);
            Assert.AreEqual("Failed", GetProperty<object>(duplicate, "State").ToString());
            Assert.AreEqual("DuplicateRequestId", GetProperty<object>(duplicate, "Error").ToString());
        }

        [Test]
        public void InitialWeaponResolveFailure_DoesNotPublishReadyAndReleasesCharacterLease()
        {
            object manager = CreateManager();
            var characterProvider = new ImmediateLeaseProvider(
                Resources.Load<CharacterDefinition>("CharacterDefinition"));
            SetField(manager, "definitionProvider", characterProvider);
            SetField(
                manager,
                "weaponDefinitionProvider",
                new FailingWeaponDefinitionProvider(
                    WeaponAnimationDefinitionResolveError.DefinitionNotFound));
            bool readyObserved = false;
            AddEventHandler(manager, "CharacterReady", _ => readyObserved = true);
            object operation = Invoke(
                manager,
                "BeginSpawn",
                CreateRequest("missing-initial-weapon", Vector3.zero));

            for (int i = 0; i < 3; i++)
            {
                Invoke(manager, "Update", 0f);
            }

            Assert.AreEqual(
                "Failed",
                GetProperty<object>(operation, "State").ToString());
            Assert.AreEqual(
                "InitialWeaponDefinitionNotFound",
                GetProperty<object>(operation, "Error").ToString());
            Assert.IsFalse(readyObserved);
            Assert.AreEqual(1, characterProvider.ReleaseCount);
            Assert.AreEqual(
                0,
                GameObject.Find("[CharacterRuntimeRoot]").transform.childCount);
        }

        [Test]
        public void CancelSpawn_ReleasesLateInitialWeaponLeaseExactlyOnce()
        {
            object manager = CreateManager();
            var characterProvider = new ImmediateLeaseProvider(
                Resources.Load<CharacterDefinition>("CharacterDefinition"));
            var weaponProvider = new DelayedWeaponDefinitionProvider();
            SetField(manager, "definitionProvider", characterProvider);
            SetField(manager, "weaponDefinitionProvider", weaponProvider);
            object operation = Invoke(
                manager,
                "BeginSpawn",
                CreateRequest("cancel-initial-weapon", Vector3.zero));

            Invoke(manager, "Update", 0f);
            Invoke(manager, "Update", 0f);
            Assert.AreEqual(
                "ResolvingInitialWeapon",
                GetProperty<object>(operation, "State").ToString());
            object request = GetProperty<object>(operation, "Request");
            object requestId = GetProperty<object>(request, "RequestId");
            Assert.IsTrue((bool)Invoke(manager, "CancelSpawn", requestId));
            Invoke(manager, "Update", 0f);

            Assert.AreEqual(
                "Cancelled",
                GetProperty<object>(operation, "State").ToString());
            Assert.AreEqual(1, characterProvider.ReleaseCount);
            Assert.AreEqual(0, weaponProvider.ReleaseCount);

            weaponProvider.Complete(
                Resources.Load<WeaponAnimationDefinition>(
                    "FistsWeaponAnimationDefinition"));

            Assert.AreEqual(1, weaponProvider.ReleaseCount);
            Assert.AreEqual(
                0,
                GameObject.Find("[CharacterRuntimeRoot]").transform.childCount);
        }

        [Test]
        public void ReleasedRequestId_IsRejectedWhileNewRequestCanSpawn()
        {
            object manager = CreateManager();
            object oldRequest = CreateRequest("released-request", Vector3.zero);
            object operation = Invoke(manager, "BeginSpawn", oldRequest);
            for (int i = 0; i < 6; i++) Invoke(manager, "Update", 0f);
            Assert.AreSame(operation, Invoke(manager, "BeginSpawn", oldRequest));
            object reason = Enum.Parse(RequireRuntimeType("CGame.CharacterDespawnReason"), "Requested");
            Assert.IsTrue((bool)Invoke(manager, "Despawn", GetProperty<object>(operation, "RuntimeId"), reason));
            Invoke(manager, "Update", 0f);
            Assert.AreEqual("Released", GetProperty<object>(operation, "State").ToString());

            object duplicate = Invoke(manager, "BeginSpawn", oldRequest);
            Assert.AreEqual("DuplicateRequestId", GetProperty<object>(duplicate, "Error").ToString());
            object fresh = Invoke(manager, "BeginSpawn", CreateRequest("fresh-request", new Vector3(2f, 0f, 0f)));
            for (int i = 0; i < 6; i++) Invoke(manager, "Update", 0f);
            Assert.AreEqual("CharacterReady", GetProperty<object>(fresh, "State").ToString());
        }

        [Test]
        public void StageFailures_RollBackLeaseAssemblyRegistrationsAndRoot()
        {
            AssertStageFailure("Assembling");
            ShutdownManagers();
            AssertStageFailure("Registering");
            ShutdownManagers();
            AssertStageFailure("Possessing");
            ShutdownManagers();
            AssertStageFailure("Activation");
        }

        [Test]
        public void TerminalRequestCache_HasFixedCapacityAndEvictsOldestId()
        {
            object manager = CreateManager();
            Type managerType = RequireRuntimeType("CGame.CharacterSpawnManager");
            int capacity = (int)managerType.GetField("TerminalRequestCapacity", BindingFlags.Public | BindingFlags.Static).GetRawConstantValue();
            Assert.AreEqual(128, capacity);
            for (int i = 0; i <= capacity; i++)
            {
                object failed = Invoke(manager, "BeginSpawn", CreateRequest($"terminal-{i}", new Vector3(float.NaN, 0f, 0f)));
                Invoke(manager, "Update", 0f);
                Assert.AreEqual("Failed", GetProperty<object>(failed, "State").ToString());
            }

            object evicted = Invoke(manager, "BeginSpawn", CreateRequest("terminal-0", Vector3.zero));
            Assert.AreEqual("Requested", GetProperty<object>(evicted, "State").ToString());
            object retained = Invoke(manager, "BeginSpawn", CreateRequest($"terminal-{capacity}", Vector3.zero));
            Assert.AreEqual("DuplicateRequestId", GetProperty<object>(retained, "Error").ToString());
        }

        private object CreateManager()
        {
            Type gameManagerType = RequireRuntimeType("CGame.GameManager");
            object manager = gameManagerType.GetMethod("CreateManager", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { RequireRuntimeType("CGame.CharacterSpawnManager") });
            if (manager == null)
            {
                throw new InvalidOperationException("CharacterSpawnManager could not be created.");
            }

            CharacterDefinition definition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            SetField(manager, "definitionProvider", new InMemoryCharacterDefinitionProvider(new[] { definition }));
            SetField(
                manager,
                "weaponDefinitionProvider",
                new InMemoryWeaponAnimationDefinitionProvider(
                    new[]
                    {
                        Resources.Load<WeaponAnimationDefinition>(
                            "FistsWeaponAnimationDefinition"),
                    }));
            return manager;
        }

        private void AssertStageFailure(string stage)
        {
            object manager = CreateManager();
            CharacterDefinition definition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            var provider = new ImmediateLeaseProvider(definition);
            SetField(manager, "definitionProvider", provider);
            InputType inputType = stage == "Possessing" ? InputType.Vehicle : InputType.Player;
            string objectName = $"Failure{stage}";
            object operation = Invoke(manager, "BeginSpawn", CreateRequest($"failure-{stage}", Vector3.zero, inputType, objectName));
            string injectionState =
                stage == "Activation" ? "Possessing" : stage;
            for (int frame = 0;
                 frame < 10
                 && GetProperty<object>(operation, "State").ToString()
                    != injectionState;
                 frame++)
            {
                Invoke(manager, "Update", 0f);
            }

            if (stage == "Assembling")
            {
                SetField(manager, "assembler", null);
            }
            else if (stage == "Registering")
            {
                SetField(manager, "pawnManager", null);
            }
            else if (stage == "Activation")
            {
                object assemblies = manager.GetType()
                    .GetField(
                        "assemblies",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(manager);
                object assembly = ((IEnumerable)assemblies)
                    .Cast<object>()
                    .Select(entry => GetProperty<object>(entry, "Value"))
                    .Single();
                UnityEngine.Object.DestroyImmediate(
                    GetProperty<GameObject>(assembly, "Root"));
            }

            Invoke(manager, "Update", 0f);

            Assert.AreEqual("Failed", GetProperty<object>(operation, "State").ToString(), stage);
            Assert.AreEqual(1, provider.ReleaseCount, stage);
            Assert.AreEqual(0, GameObject.Find("[CharacterRuntimeRoot]").transform.childCount, stage);
            Assert.IsNull(GameObject.Find(objectName), stage);
        }

        private static void ShutdownManagers()
        {
            Type gameManagerType = RequireRuntimeType("CGame.GameManager");
            object managerList = gameManagerType.GetField("managerList", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            if (managerList == null)
            {
                return;
            }

            var managers = new List<object>();
            foreach (object manager in (IEnumerable)managerList)
            {
                managers.Add(manager);
            }

            for (int i = managers.Count - 1; i >= 0; i--)
            {
                Invoke(managers[i], "Shutdown");
            }

            managerList.GetType().GetMethod("Clear")?.Invoke(managerList, null);
        }

        private static object CreateRequest(string requestId, Vector3 position, InputType inputType = InputType.Player, string displayName = "RuntimeCharacter")
        {
            CharacterDefinition definition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            Assert.NotNull(definition);
            Type requestType = RequireRuntimeType("CGame.CharacterSpawnRequest");
            object typedRequestId = Activator.CreateInstance(RequireRuntimeType("CGame.CharacterSpawnRequestId"), requestId);
            object placement = CreatePlacement(position, Quaternion.identity);
            return Activator.CreateInstance(requestType, typedRequestId, definition.DefinitionId, CharacterControlKind.LocalPlayer, placement, inputType, displayName);
        }

        private static object CreatePlacement(Vector3 position, Quaternion rotation)
        {
            return Activator.CreateInstance(RequireRuntimeType("CGame.CharacterSpawnPlacement"), position, rotation);
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

        private static void AddEventHandler(object target, string eventName, Action<object[]> callback)
        {
            EventInfo eventInfo = target.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            MethodInfo invokeMethod = eventInfo.EventHandlerType.GetMethod("Invoke");
            ParameterInfo[] eventParameters = invokeMethod.GetParameters();
            ParameterExpression[] parameters = eventParameters
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();
            NewArrayExpression arguments = Expression.NewArrayInit(typeof(object), parameters.Select(parameter => Expression.Convert(parameter, typeof(object))));
            MethodCallExpression body = Expression.Call(Expression.Constant(callback), typeof(Action<object[]>).GetMethod("Invoke"), arguments);
            Delegate handler = Expression.Lambda(eventInfo.EventHandlerType, body, parameters).Compile();
            eventInfo.AddEventHandler(target, handler);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Runtime type was not found: {fullName}");
            return type;
        }

        private sealed class DelayedDefinitionProvider : ICharacterDefinitionProvider
        {
            private readonly CharacterDefinitionResolveOperation operation = new CharacterDefinitionResolveOperation();

            public int ReleaseCount { get; private set; }
            public ICharacterDefinitionResolveOperation BeginResolve(CharacterDefinitionId definitionId) => operation;
            public CharacterDefinitionResolveResult Resolve(CharacterDefinitionId definitionId) => throw new NotSupportedException();

            public void Complete(CharacterDefinition definition)
            {
                operation.Complete(new CharacterDefinitionResolveResult(
                    new ResolvedCharacterDefinitionLease(definition, () => ReleaseCount++),
                    CharacterDefinitionResolveError.None));
            }
        }

        private sealed class ImmediateLeaseProvider : ICharacterDefinitionProvider
        {
            private readonly CharacterDefinition definition;

            public ImmediateLeaseProvider(CharacterDefinition definition)
            {
                this.definition = definition;
            }

            public int ReleaseCount { get; private set; }

            public ICharacterDefinitionResolveOperation BeginResolve(CharacterDefinitionId definitionId)
            {
                var operation = new CharacterDefinitionResolveOperation();
                operation.Complete(Resolve(definitionId));
                return operation;
            }

            public CharacterDefinitionResolveResult Resolve(CharacterDefinitionId definitionId)
            {
                return new CharacterDefinitionResolveResult(
                    new ResolvedCharacterDefinitionLease(definition, () => ReleaseCount++),
                    CharacterDefinitionResolveError.None);
            }
        }

        private sealed class FailingWeaponDefinitionProvider :
            IWeaponAnimationDefinitionProvider
        {
            private readonly WeaponAnimationDefinitionResolveError error;

            public FailingWeaponDefinitionProvider(
                WeaponAnimationDefinitionResolveError error)
            {
                this.error = error;
            }

            public IWeaponAnimationDefinitionResolveOperation BeginResolve(
                WeaponId weaponId)
            {
                return WeaponAnimationDefinitionResolveOperation.Completed(
                    new WeaponAnimationDefinitionResolveResult(
                        (ResolvedWeaponAnimationDefinitionLease)null,
                        error));
            }

            public void Dispose()
            {
            }
        }

        private sealed class DelayedWeaponDefinitionProvider :
            IWeaponAnimationDefinitionProvider
        {
            private readonly WeaponAnimationDefinitionResolveOperation
                operation = new WeaponAnimationDefinitionResolveOperation();

            public int ReleaseCount { get; private set; }

            public IWeaponAnimationDefinitionResolveOperation BeginResolve(
                WeaponId weaponId)
            {
                return operation;
            }

            public void Complete(WeaponAnimationDefinition definition)
            {
                operation.Complete(
                    new WeaponAnimationDefinitionResolveResult(
                        new ResolvedWeaponAnimationDefinitionLease(
                            definition,
                            () => ReleaseCount++),
                        WeaponAnimationDefinitionResolveError.None));
            }

            public void Dispose()
            {
                operation.Dispose();
            }
        }
    }
}
