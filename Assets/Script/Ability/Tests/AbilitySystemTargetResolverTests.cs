using CGame.Ability.Targeting;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class AbilitySystemTargetResolverTests
    {
        [Test]
        public void Resolver_ResolvesRootAndChildColliderOnlyWhileAvatarIsCurrentAndActive()
        {
            var root = new GameObject("TargetRoot");
            var child = new GameObject("HitBox");
            child.transform.SetParent(root.transform);
            BoxCollider rootCollider = root.AddComponent<BoxCollider>();
            BoxCollider childCollider = child.AddComponent<BoxCollider>();
            var avatar = new TestAvatar(root);
            var abilitySystem = new AbilitySystemComponent(avatar, avatar, null);
            var resolver = new AbilitySystemTargetResolver();

            Assert.That(resolver.TryResolve(rootCollider, out AbilitySystemComponent rootResult), Is.True);
            Assert.That(rootResult, Is.SameAs(abilitySystem));
            Assert.That(resolver.TryResolve(childCollider, out AbilitySystemComponent childResult), Is.True);
            Assert.That(childResult, Is.SameAs(abilitySystem));

            root.SetActive(false);
            Assert.That(resolver.TryResolve(childCollider, out _), Is.False);
            root.SetActive(true);
            abilitySystem.SetAvatar(null);
            Assert.That(resolver.TryResolve(rootCollider, out _), Is.False);

            abilitySystem.Dispose();
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Resolver_RejectsWallCollider()
        {
            var wall = new GameObject("Wall");
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            var resolver = new AbilitySystemTargetResolver();

            Assert.That(resolver.TryResolve(collider, out _), Is.False);

            Object.DestroyImmediate(wall);
        }

        private sealed class TestAvatar : IAbilitySystemAvatar
        {
            public TestAvatar(GameObject root)
            {
                AbilitySystemRoot = root;
            }

            public GameObject AbilitySystemRoot { get; }
        }
    }
}
