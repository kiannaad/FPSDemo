using System;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    [Category("Network047")]
    public sealed class DedicatedTargetIdentityRegistryTests
    {
        [Test]
        public void Resolve_EnemyCollider_ReturnsRegisteredLevelPointId()
        {
            var target = new GameObject("Target");
            try
            {
                BoxCollider collider = target.AddComponent<BoxCollider>();
                var registry = new DedicatedTargetIdentityRegistry();

                registry.Register("EnemyPoint 1", target);

                Assert.That(registry.Resolve(collider), Is.EqualTo("EnemyPoint 1"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Resolve_WallCollider_ReturnsNull()
        {
            var wall = new GameObject("Wall");
            try
            {
                BoxCollider collider = wall.AddComponent<BoxCollider>();
                var registry = new DedicatedTargetIdentityRegistry();

                Assert.That(registry.Resolve(collider), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void Register_DuplicatePointId_RejectsDedicatedStartup()
        {
            var firstTarget = new GameObject("FirstTarget");
            var secondTarget = new GameObject("SecondTarget");
            try
            {
                firstTarget.AddComponent<BoxCollider>();
                secondTarget.AddComponent<BoxCollider>();
                var registry = new DedicatedTargetIdentityRegistry();
                registry.Register("EnemyPoint 1", firstTarget);

                Assert.Throws<InvalidOperationException>(() => registry.Register("EnemyPoint 1", secondTarget));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstTarget);
                UnityEngine.Object.DestroyImmediate(secondTarget);
            }
        }
    }
}
