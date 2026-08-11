using System;
using CGame.Animation.Rig;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Animation.RigFoundation.Tests
{
    public sealed class KRigFoundationTests
    {
        private GameObject root;
        private KRig rig;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Root");
            new GameObject("Spine").transform.SetParent(root.transform);
            root.AddComponent<Animator>();
            rig = ScriptableObject.CreateInstance<KRig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(rig);
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Initialize_MatchingImportedHierarchy_ResolvesOnlyByIndex()
        {
            KRigComponent component = root.AddComponent<KRigComponent>();
            rig.Import(component);

            component.Initialize(rig);

            Assert.That(component.IsInitialized, Is.True);
            Assert.That(component.GetRigTransform(rig.Hierarchy[0]), Is.EqualTo(root.transform));
            Assert.That(component.GetRigTransform(rig.Hierarchy[1]).name, Is.EqualTo("Spine"));
        }

        [Test]
        public void Initialize_ChangedHierarchy_ThrowsInsteadOfFallingBackByName()
        {
            KRigComponent component = root.AddComponent<KRigComponent>();
            rig.Import(component);
            root.transform.GetChild(0).name = "RenamedSpine";
            component.RefreshHierarchy();

            Assert.That(
                () => component.Initialize(rig),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Initialize_VirtualElementWithoutTarget_Throws()
        {
            KRigComponent component = root.AddComponent<KRigComponent>();
            root.AddComponent<KVirtualElement>();
            rig.Import(component);

            Assert.That(
                () => component.Initialize(rig),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GetRigTransform_BeforeInitialization_Throws()
        {
            KRigComponent component = root.AddComponent<KRigComponent>();

            Assert.That(
                () => component.GetRigTransform(0),
                Throws.TypeOf<InvalidOperationException>());
        }
    }
}
