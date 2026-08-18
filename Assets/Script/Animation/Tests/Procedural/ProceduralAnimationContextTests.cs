using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Animation.Tests
{
    public sealed class ProceduralAnimationContextTests
    {
        private GameObject root;
        private Pawn pawn;
        private AnimationUpdateContext context;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(ProceduralAnimationContextTests));
            pawn = new Pawn(root);
            context = new AnimationUpdateContext(
                pawn,
                new TestAnimationSource(root.transform));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Update_ReadsPawnOwnedProceduralFactsAsReadOnlyFrameData()
        {
            Pose aimOffset = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(5f, 10f, 15f));
            Pose recoilOffset = new Pose(new Vector3(0.1f, 0.2f, 0.3f), Quaternion.Euler(1f, 2f, 3f));
            InvokePawn("SetAimAnimationFacts", true, aimOffset);
            root.transform.rotation = Quaternion.Euler(0f, 10f, 0f);
            InvokePawn("ApplyingPresentationRotation", Quaternion.Euler(-15f, 55f, 0f));
            InvokePawn("SetViewAnimationFacts",
                Vector2.zero,
                new Vector2(2f, -3f),
                8f,
                true);
            InvokePawn("SetLookLayerWeight", 0.75f);
            InvokePawn("SubmitControlIntent", new CharacterControlIntent(new Vector3(2f, 0f, 2f), false, false));
            InvokePawn("SetRecoilAnimationFact", recoilOffset);
            InvokePawn("SetWeaponCollisionAnimationFacts", true, 1.25f);

            UpdateContext();

            Assert.That(context.IsAiming, Is.True);
            Assert.That(context.AimPointOffset.Position, Is.EqualTo(aimOffset.position));
            Assert.That(Quaternion.Angle(context.AimPointOffset.Rotation, aimOffset.rotation), Is.LessThan(0.0001f));
            Assert.That(
                Vector2.Distance(context.ViewAnglesDegrees, new Vector2(45f, -15f)),
                Is.LessThan(0.001f));
            Assert.That(context.ViewDeltaDegrees, Is.EqualTo(new Vector2(2f, -3f)));
            Assert.That(context.LookLayerWeight, Is.EqualTo(0.75f));
            Assert.That(context.LeanAngleDegrees, Is.EqualTo(8f));
            Assert.That(
                Vector2.Distance(context.MoveInput, new Vector2(0.70710677f, 0.70710677f)),
                Is.LessThan(0.00001f));
            Assert.That(context.UseFreeAim, Is.True);
            Assert.That(context.RecoilOffset.Position, Is.EqualTo(recoilOffset.position));
            Assert.That(context.WeaponCollisionHasHit, Is.True);
            Assert.That(context.WeaponCollisionDistance, Is.EqualTo(1.25f));
        }

        [Test]
        public void Update_CalculatesSignedRelativeYawAndControlPitchForLook()
        {
            root.transform.rotation = Quaternion.Euler(0f, 170f, 0f);
            InvokePawn("ApplyingPresentationRotation", Quaternion.Euler(20f, -170f, 0f));
            InvokePawn("SetLookLayerWeight", 0f);

            UpdateContext();

            Assert.That(context.ViewAnglesDegrees.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(context.ViewAnglesDegrees.y, Is.EqualTo(20f).Within(0.001f));
            Assert.That(context.LookLayerWeight, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void Update_InvalidProceduralFactsFallBackToNeutralValues()
        {
            float invalid = float.NaN;
            InvokePawn("SetAimAnimationFacts", true, new Pose(new Vector3(invalid, 0f, 0f), new Quaternion(0f, 0f, 0f, 0f)));
            InvokePawn("SetViewAnimationFacts",
                new Vector2(float.PositiveInfinity, 1f),
                new Vector2(1f, float.NegativeInfinity),
                invalid,
                true);
            InvokePawn("SubmitControlIntent", new CharacterControlIntent(new Vector3(invalid, 0f, 1f), false, false));
            InvokePawn("SetRecoilAnimationFact", new Pose(Vector3.zero, new Quaternion(0f, 0f, 0f, 0f)));
            InvokePawn("SetWeaponCollisionAnimationFacts", true, -1f);

            UpdateContext();

            Assert.That(context.AimPointOffset.Equals(KTransform.Identity, true), Is.True);
            Assert.That(context.ViewAnglesDegrees, Is.EqualTo(Vector2.zero));
            Assert.That(context.ViewDeltaDegrees, Is.EqualTo(Vector2.zero));
            Assert.That(context.LeanAngleDegrees, Is.Zero);
            Assert.That(context.MoveInput, Is.EqualTo(Vector2.zero));
            Assert.That(context.RecoilOffset.Equals(KTransform.Identity, true), Is.True);
            Assert.That(context.WeaponCollisionHasHit, Is.False);
            Assert.That(context.WeaponCollisionDistance, Is.Zero);
        }

        [Test]
        public void RotationContract_PresentationChangesUntilNextPrePhysicsLatch()
        {
            Quaternion firstPresentation = Quaternion.Euler(-10f, 35f, 0f);
            Quaternion secondPresentation = Quaternion.Euler(20f, 80f, 0f);

            InvokePawn("ApplyingPresentationRotation", firstPresentation);
            Assert.That(Quaternion.Angle(GetPawnRotation("PresentationRotation"), firstPresentation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(GetPawnRotation("SimulatedRotation"), Quaternion.identity), Is.LessThan(0.001f));

            LatchSimulatedRotation();
            Assert.That(Quaternion.Angle(GetPawnRotation("SimulatedRotation"), firstPresentation), Is.LessThan(0.001f));

            InvokePawn("ApplyingPresentationRotation", secondPresentation);
            Assert.That(Quaternion.Angle(GetPawnRotation("PresentationRotation"), secondPresentation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(GetPawnRotation("SimulatedRotation"), firstPresentation), Is.LessThan(0.001f));

            LatchSimulatedRotation();
            Assert.That(Quaternion.Angle(GetPawnRotation("SimulatedRotation"), secondPresentation), Is.LessThan(0.001f));
        }

        [Test]
        public void RotationContract_ResetClearsPresentationAndSimulation()
        {
            InvokePawn("ApplyingPresentationRotation", Quaternion.Euler(0f, 90f, 0f));
            LatchSimulatedRotation();

            InvokePawn("ResetRotationState");

            Assert.That(Quaternion.Angle(GetPawnRotation("PresentationRotation"), Quaternion.identity), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(GetPawnRotation("SimulatedRotation"), Quaternion.identity), Is.LessThan(0.001f));
        }

        private void UpdateContext()
        {
            typeof(AnimationUpdateContext)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(context, new object[] { 0.016f });
        }

        private void InvokePawn(string methodName, params object[] arguments)
        {
            typeof(Pawn).GetMethod(methodName).Invoke(pawn, arguments);
        }

        private void LatchSimulatedRotation()
        {
            typeof(Pawn)
                .GetMethod("LatchSimulatedRotation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pawn, new object[] { 0.02f });
        }

        private Quaternion GetPawnRotation(string propertyName)
        {
            return (Quaternion)typeof(Pawn).GetProperty(propertyName).GetValue(pawn, null);
        }

        private sealed class TestAnimationSource : IAnimationCharacterSource
        {
            public TestAnimationSource(Transform transform)
            {
                Transform = transform;
            }

            public Transform Transform { get; }
            public Vector3 Velocity => Vector3.zero;
            public bool IsGrounded => true;
        }
    }
}
