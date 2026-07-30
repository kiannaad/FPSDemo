using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

            GameObject runtimeRoot =
                GameObject.Find("[CharacterRuntimeRoot]");
            if (runtimeRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }
        }

        [Test]
        public void SpawnIdentifiers_KeepRequestAndRuntimeIdentitySeparate()
        {
            Type requestIdType =
                RequireRuntimeType("CGame.CharacterSpawnRequestId");
            Type runtimeIdType =
                RequireRuntimeType("CGame.CharacterRuntimeId");
            object requestA =
                Activator.CreateInstance(requestIdType, "request-a");
            object requestACopy =
                Activator.CreateInstance(requestIdType, "request-a");
            object runtimeA =
                Activator.CreateInstance(runtimeIdType, "runtime-a");

            Assert.IsTrue(GetProperty<bool>(requestA, "IsValid"));
            Assert.AreEqual(requestA, requestACopy);
            Assert.IsTrue(GetProperty<bool>(runtimeA, "IsValid"));
            Assert.AreNotEqual(
                GetProperty<string>(requestA, "Value"),
                GetProperty<string>(runtimeA, "Value"));
        }

        [Test]
        public void SpawnPlacement_RejectsNonFiniteTransforms()
        {
            Assert.IsTrue(GetProperty<bool>(
                CreatePlacement(Vector3.zero, Quaternion.identity),
                "IsValid"));
            Assert.IsFalse(GetProperty<bool>(
                CreatePlacement(
                    new Vector3(float.NaN, 0f, 0f),
                    Quaternion.identity),
                "IsValid"));
        }

        [Test]
        public void BeginSpawn_OnlyRegistersRequestUntilManagerUpdate()
        {
            object manager = CreateManager();
            object operation = Invoke(
                manager,
                "BeginSpawn",
                CreateRequest(
                    "deferred-request",
                    Vector3.zero));

            Assert.AreEqual(
                "Requested",
                GetProperty<object>(operation, "State").ToString());
            Assert.AreEqual(
                0,
                GameObject.Find("[CharacterRuntimeRoot]")
                    .transform.childCount);
        }

        [Test]
        public void Update_RejectsInvalidPlacementBeforeAssetLoading()
        {
            object manager = CreateManager();
            object operation = Invoke(
                manager,
                "BeginSpawn",
                CreateRequest(
                    "invalid-placement",
                    new Vector3(float.NaN, 0f, 0f)));

            Invoke(manager, "Update", 0f);

            Assert.AreEqual(
                "Failed",
                GetProperty<object>(operation, "State").ToString());
            Assert.AreEqual(
                "InvalidPlacement",
                GetProperty<object>(operation, "Error").ToString());
        }

        [Test]
        public void RegistrationHandles_AreStableAndIdempotent()
        {
            object pawnManager =
                Activator.CreateInstance(
                    RequireRuntimeType("CGame.PawnManager"));
            object pawn =
                Activator.CreateInstance(
                    RequireRuntimeType("CGame.Pawn"));
            object first =
                Invoke(pawnManager, "RegisterPawn", pawn);
            object duplicate =
                Invoke(pawnManager, "RegisterPawn", pawn);

            Assert.AreSame(first, duplicate);
            Assert.IsTrue(GetProperty<bool>(first, "IsActive"));
            Invoke(first, "Dispose");
            Invoke(first, "Dispose");
            Assert.IsFalse(GetProperty<bool>(first, "IsActive"));
        }

        [Test]
        public void TerminalRequestCache_EvictsOldestInvalidRequest()
        {
            object manager = CreateManager();
            Type managerType =
                RequireRuntimeType("CGame.CharacterSpawnManager");
            int capacity = (int)managerType
                .GetField(
                    "TerminalRequestCapacity",
                    BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();

            for (int i = 0; i <= capacity; i++)
            {
                object failed = Invoke(
                    manager,
                    "BeginSpawn",
                    CreateRequest(
                        $"terminal-{i}",
                        new Vector3(float.NaN, 0f, 0f)));
                Invoke(manager, "Update", 0f);
                Assert.AreEqual(
                    "Failed",
                    GetProperty<object>(failed, "State").ToString());
            }

            object evicted = Invoke(
                manager,
                "BeginSpawn",
                CreateRequest("terminal-0", Vector3.zero));
            Assert.AreEqual(
                "Requested",
                GetProperty<object>(evicted, "State").ToString());
        }

        private static object CreateManager()
        {
            Type gameManagerType =
                RequireRuntimeType("CGame.GameManager");
            object manager = gameManagerType
                .GetMethod(
                    "CreateManager",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(
                    null,
                    new[]
                    {
                        RequireRuntimeType(
                            "CGame.CharacterSpawnManager"),
                    });
            Assert.NotNull(manager);
            return manager;
        }

        private static object CreateRequest(
            string requestId,
            Vector3 position)
        {
            CharacterDefinition definition =
                Resources.Load<CharacterDefinition>(
                    "CharacterDefinition");
            Assert.NotNull(definition);
            return Activator.CreateInstance(
                RequireRuntimeType("CGame.CharacterSpawnRequest"),
                Activator.CreateInstance(
                    RequireRuntimeType(
                        "CGame.CharacterSpawnRequestId"),
                    requestId),
                definition.DefinitionId,
                CharacterControlKind.LocalPlayer,
                CreatePlacement(position, Quaternion.identity),
                InputType.Player,
                "RuntimeCharacter");
        }

        private static object CreatePlacement(
            Vector3 position,
            Quaternion rotation)
        {
            return Activator.CreateInstance(
                RequireRuntimeType(
                    "CGame.CharacterSpawnPlacement"),
                position,
                rotation);
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            return target.GetType()
                .GetMethod(
                    methodName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }

        private static T GetProperty<T>(
            object target,
            string propertyName)
        {
            return (T)target.GetType()
                .GetProperty(
                    propertyName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(
                type,
                $"Runtime type was not found: {fullName}");
            return type;
        }

        private static void ShutdownManagers()
        {
            Type gameManagerType =
                RequireRuntimeType("CGame.GameManager");
            object managerList = gameManagerType
                .GetField(
                    "managerList",
                    BindingFlags.Static | BindingFlags.NonPublic)
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

            managerList.GetType()
                .GetMethod("Clear")
                ?.Invoke(managerList, null);
        }
    }
}
