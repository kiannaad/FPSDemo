using NUnit.Framework;
using CGame.Animation.Rig;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation.Tests
{
    public sealed class WeaponPoseLayerParityTests
    {
        [Test]
        public void TurnRuntimeState_ContinuouslyOffsetsModelRootBeforeAndAcrossTheYawThreshold()
        {
            TurnRuntimeState state = default;
            AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            TurnRequest preThresholdRequest = state.Advance(-45f, 1f / 60f, 70f, 1.1f, curve);

            Assert.That(preThresholdRequest, Is.EqualTo(TurnRequest.None));
            Assert.That(state.IsTurning, Is.False);
            Assert.That(state.Angle, Is.EqualTo(45f).Within(0.001f));
            Assert.That(state.AppliedAngle, Is.EqualTo(45f).Within(0.001f),
                "Free-look yaw must continuously counter-rotate the visual ModelRoot before turn-in-place begins.");

            TurnRequest thresholdRequest = state.Advance(-30f, 1f / 60f, 70f, 1.1f, curve);

            Assert.That(thresholdRequest, Is.EqualTo(TurnRequest.Left));
            Assert.That(state.IsTurning, Is.True);
            Assert.That(state.AppliedAngle, Is.GreaterThan(0f).And.LessThanOrEqualTo(75f),
                "Crossing the threshold must retain the existing counter-offset and only then begin easing it to zero.");
        }

        [Test]
        public void KTransform_RelativeWorldAndLerpMathIsDeterministic()
        {
            System.Random random = new System.Random(13082026);
            for (int index = 0; index < 64; index++)
            {
                KTransform parent = RandomTransform(random);
                KTransform child = RandomTransform(random);
                KTransform relative = parent.GetRelativeTransform(child, false);
                KTransform reconstructed = parent.GetWorldTransform(relative, false);
                AssertVector(reconstructed.Position, child.Position);
                Assert.That(
                    Quaternion.Angle(reconstructed.Rotation, child.Rotation),
                    Is.LessThan(0.001f));

                KTransform interpolated = KTransform.Lerp(parent, child, 0.37f);
                AssertVector(
                    interpolated.Position,
                    Vector3.Lerp(parent.Position, child.Position, 0.37f));
                AssertVector(
                    interpolated.Scale,
                    Vector3.Lerp(parent.Scale, child.Scale, 0.37f));
                Assert.That(
                    Quaternion.Angle(
                        interpolated.Rotation,
                        Quaternion.Slerp(parent.Rotation, child.Rotation, 0.37f)),
                    Is.LessThan(0.001f));
            }
        }

        [Test]
        public void TwoBoneIk_PreservesLengthsAndReachesTheWeightedTargetDeterministically()
        {
            for (int index = 0; index < 32; index++)
            {
                float offset = index * 0.013f;
                KTwoBoneIkData actual = new KTwoBoneIkData
                {
                    Root = new KTransform(new Vector3(offset, 0f, 0f), Quaternion.Euler(2f, -4f, 1f)),
                    Mid = new KTransform(new Vector3(0.18f + offset, 0.04f, 0.03f), Quaternion.Euler(8f, 3f, -2f)),
                    Tip = new KTransform(new Vector3(0.36f + offset, 0.01f, 0.08f), Quaternion.Euler(-3f, 6f, 4f)),
                    Target = new KTransform(new Vector3(0.28f + offset, 0.12f, 0.2f), Quaternion.Euler(15f, -11f, 9f)),
                    Hint = new KTransform(new Vector3(0.1f + offset, 0.3f, -0.08f), Quaternion.identity),
                    PositionWeight = 0.25f + 0.7f * index / 31f,
                    RotationWeight = 0.8f,
                    HintWeight = 0.6f,
                    HasValidHint = true
                };
                KTwoBoneIkData repeated = actual;
                float upperLength = Vector3.Distance(actual.Root.Position, actual.Mid.Position);
                float lowerLength = Vector3.Distance(actual.Mid.Position, actual.Tip.Position);
                Vector3 weightedTarget = Vector3.Lerp(
                    actual.Tip.Position,
                    actual.Target.Position,
                    actual.PositionWeight);
                Quaternion weightedRotation = Quaternion.Slerp(
                    actual.Tip.Rotation,
                    actual.Target.Rotation,
                    actual.RotationWeight);

                KTwoBoneIK.Solve(ref actual);
                KTwoBoneIK.Solve(ref repeated);

                Assert.That(
                    Vector3.Distance(actual.Root.Position, actual.Mid.Position),
                    Is.EqualTo(upperLength).Within(0.0001f));
                Assert.That(
                    Vector3.Distance(actual.Mid.Position, actual.Tip.Position),
                    Is.EqualTo(lowerLength).Within(0.0001f));
                Assert.That(
                    Vector3.Distance(actual.Tip.Position, weightedTarget),
                    Is.LessThan(0.0002f));
                Assert.That(
                    Quaternion.Angle(actual.Tip.Rotation, weightedRotation),
                    Is.LessThan(0.001f));
                AssertPose(actual.Root, repeated.Root);
                AssertPose(actual.Mid, repeated.Mid);
                AssertPose(actual.Tip, repeated.Tip);
            }
        }

        [Test]
        public void ChainIk_ReachesOrClampsTargetsWhilePreservingSegmentLengths()
        {
            Vector3[] targets =
            {
                new Vector3(1.2f, 0.8f, 0.1f),
                new Vector3(4f, 1f, -0.5f)
            };
            foreach (Vector3 target in targets)
            {
                Vector3[] positions = { Vector3.zero, Vector3.right, Vector3.right * 2f };
                float[] lengths = { 1f, 1f };
                ChainIkData actual = new ChainIkData
                {
                    Positions = (Vector3[])positions.Clone(),
                    Lengths = (float[])lengths.Clone(),
                    Target = target,
                    Tolerance = 0.0001f,
                    MaxReach = 2f,
                    MaxIterations = 20
                };
                Assert.That(KChainIK.SolveFabrik(ref actual), Is.True);
                Assert.That(
                    Vector3.Distance(actual.Positions[0], actual.Positions[1]),
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    Vector3.Distance(actual.Positions[1], actual.Positions[2]),
                    Is.EqualTo(1f).Within(0.0001f));
                Vector3 expectedTip = target.magnitude <= 2f
                    ? target
                    : target.normalized * 2f;
                Assert.That(
                    Vector3.Distance(actual.Positions[2], expectedTip),
                    Is.LessThan(0.001f));
            }
        }

        [Test]
        public void FiveLayerJobs_AreConcreteAndDeclareTheirExactSettingsTypes()
        {
            AssertJob<PoseSamplerLayerSettings, PoseSamplerLayerJob>();
            AssertJob<PoseOffsetLayerSettings, PoseOffsetLayerJob>();
            AssertJob<AttachHandLayerSettings, AttachHandLayerJob>();
            AssertJob<ViewLayerSettings, ViewLayerJob>();
            AssertJob<IkLayerSettings, IkLayerJob>();
            AssertJob<BlendingLayerSettings, BlendingLayerJob>();
            AssertJob<LookLayerSettings, LookLayerJob>();
            AssertJob<TurnLayerSettings, TurnLayerJob>();
            AssertJob<SwayLayerSettings, SwayLayerJob>();
            AssertJob<AdsLayerSettings, AdsLayerJob>();
            AssertJob<AdditiveLayerSettings, AdditiveLayerJob>();
            AssertJob<IkMotionLayerSettings, IkMotionLayerJob>();
            AssertJob<CollisionLayerSettings, CollisionLayerJob>();
        }

        [Test]
        public void CollisionRuntime_UsesHitDistanceBoundaryAndSelectedPose()
        {
            CollisionLayerSettings settings = ScriptableObject.CreateInstance<CollisionLayerSettings>();
            try
            {
                SetField(settings, "primaryPose", new KTransform(new Vector3(0f, 0f, -0.4f), Quaternion.Euler(0f, 30f, 0f)));
                SetField(settings, "secondaryPose", new KTransform(new Vector3(0f, -0.2f, 0f), Quaternion.Euler(20f, 0f, 0f)));
                SetField(settings, "barrelLength", 0.8f);
                SetField(settings, "rayStartOffset", 0.2f);
                SetField(settings, "smoothingSpeed", 0f);
                CollisionRuntimeState state = default;
                state.Advance(true, 0.5f, 1f / 60f, settings);
                Assert.That(state.BlockingPose.Position.z, Is.EqualTo(-0.2f).Within(0.0001f));
                state.Advance(true, 1f, 1f / 60f, settings);
                Assert.That(state.BlockingPose.Position.magnitude, Is.LessThan(0.0001f));
                state.Advance(false, 0f, 1f / 60f, settings);
                Assert.That(state.BlockingPose.Position.magnitude, Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void IkMotionRuntime_UsesTrueCurveLengthAndPlayRate()
        {
            IkMotionLayerSettings settings = ScriptableObject.CreateInstance<IkMotionLayerSettings>();
            try
            {
                SetField(settings, "rotationCurves", VectorCurve.Constant(0f, 2f, 0f));
                SetField(settings, "translationCurves", VectorCurve.Linear(0f, 2f, 0f, 2f));
                SetField(settings, "playRate", 2f);
                SetField(settings, "blendTime", 0f);
                SetField(settings, "autoBlendOut", false);
                SetField(settings, "translationScale", Vector3.one);
                SetField(settings, "rotationScale", Vector3.one);
                IkMotionRuntimeState state = default;
                state.Play();
                state.Advance(0.5f, settings);
                Assert.That(state.Playback, Is.EqualTo(1f).Within(0.0001f));
                state.Advance(0.5f, settings);
                Assert.That(state.Playback, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(state.IsPlaying, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void AdsRuntime_UsesProvidedDeltaTimeForAimAndAimPointTransitions()
        {
            AdsRuntimeState state = default;
            KTransform target = new KTransform(new Vector3(0.1f, 0.2f, 0.3f), Quaternion.Euler(0f, 10f, 0f));
            state.Advance(true, target, 0.25f, 2f, 2f, EaseMode.Linear);
            Assert.That(state.AimingWeight, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(Vector3.Distance(state.AimPoint.Position, target.Position * 0.5f), Is.LessThan(0.0001f));
            state.Advance(false, target, 0.25f, 2f, 2f, EaseMode.Linear);
            Assert.That(state.AimingWeight, Is.Zero.Within(0.0001f));
            Assert.That(Vector3.Distance(state.AimPoint.Position, target.Position), Is.LessThan(0.0001f));
        }

        [Test]
        public void TurnRuntime_UsesProvidedDeltaTimeAndReturnsOnlyOneRequest()
        {
            TurnRuntimeState state = default;
            AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            Assert.That(state.Advance(-40f, 0f, 30f, 1f, curve), Is.EqualTo(TurnRequest.Left));
            Assert.That(state.Angle, Is.EqualTo(40f).Within(0.0001f));
            Assert.That(state.Advance(0f, 0.5f, 30f, 1f, curve), Is.EqualTo(TurnRequest.None));
            Assert.That(state.Angle, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(state.Advance(0f, 0.5f, 30f, 1f, curve), Is.EqualTo(TurnRequest.None));
            Assert.That(state.Angle, Is.Zero.Within(0.0001f));
            Assert.That(state.IsTurning, Is.False);
        }

        [Test]
        public void SwayRuntime_ClampsFreeAimAndUsesFixedFrameData()
        {
            SwayLayerSettings settings = ScriptableObject.CreateInstance<SwayLayerSettings>();
            try
            {
                SetField(settings, "freeAimClamp", 5f);
                SetField(settings, "freeAimInterpSpeed", 100f);
                SetField(settings, "freeAimInputScale", 2f);
                SetField(settings, "movePositionSpring", VectorSpring.Identity);
                SetField(settings, "moveRotationSpring", VectorSpring.Identity);
                SetField(settings, "aimPositionSpring", VectorSpring.Identity);
                SetField(settings, "aimRotationSpring", VectorSpring.Identity);
                SwayRuntimeState state = default;
                state.Advance(new Vector2(10f, -10f), Vector2.one, true, 1f, settings);
                Assert.That(state.FreeAimValue.x, Is.EqualTo(5f).Within(0.001f));
                Assert.That(state.FreeAimValue.y, Is.EqualTo(-5f).Within(0.001f));
                state.Advance(Vector2.zero, Vector2.zero, false, 1f, settings);
                Assert.That(state.FreeAimValue.magnitude, Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BlendingSettings_RequireDesiredPoseAndRejectDuplicateResolvedBones()
        {
            GameObject root = new GameObject("BlendingSettingsRoot");
            Transform bone = CreateChild(root.transform, "Bone");
            KRigComponent rigComponent = root.AddComponent<KRigComponent>();
            KRig rig = ScriptableObject.CreateInstance<KRig>();
            BlendingLayerSettings settings = ScriptableObject.CreateInstance<BlendingLayerSettings>();
            AnimationClip clip = new AnimationClip();
            try
            {
                rigComponent.RefreshHierarchy();
                rig.Import(rigComponent);
                settings.Configure(rig);
                KRigElement element = rig.Hierarchy[1];
                var entries = new List<BlendingLayerElement>
                {
                    new BlendingLayerElement { Element = element, Weight = 0.5f }
                };
                SetField(settings, "blendingElements", entries);

                Assert.That(() => settings.Validate(rig), Throws.InvalidOperationException);

                SetField(settings, "desiredPose", clip);
                Assert.That(() => settings.Validate(rig), Throws.Nothing);

                entries.Add(new BlendingLayerElement { Element = element, Weight = 1f });
                Assert.That(() => settings.Validate(rig), Throws.InvalidOperationException);
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(AnimationCurveBlendMode.Direct, 0f, 0.25f, 0.25f)]
        [TestCase(AnimationCurveBlendMode.Direct, 2f, 0.25f, 1f)]
        [TestCase(AnimationCurveBlendMode.Mask, 0f, 0.25f, 1f)]
        [TestCase(AnimationCurveBlendMode.Mask, 0.6f, 0.25f, 0.55f)]
        public void CurveBlend_MatchesSourceClampAndMaskSemantics(
            AnimationCurveBlendMode mode,
            float curveValue,
            float clampMinimum,
            float expected)
        {
            AnimationCurveBlend blend = new AnimationCurveBlend();
            SetField(blend, "mode", mode);
            SetField(blend, "clampMinimum", clampMinimum);
            Assert.That(blend.Evaluate(curveValue), Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void PoseSamplerLayerJob_CopiesOnlyTheOwnerCurveValueIntoJobData()
        {
            PoseSamplerLayerSettings settings = ScriptableObject.CreateInstance<PoseSamplerLayerSettings>();
            PlayableGraph graph = PlayableGraph.Create("PoseSamplerCurveProvider");
            GameObject root = new GameObject("PoseSamplerOwnerCurve");
            Animator animator = root.AddComponent<Animator>();
            KRigComponent rigComponent = root.AddComponent<KRigComponent>();
            KRig rig = ScriptableObject.CreateInstance<KRig>();
            rig.Import(rigComponent);
            rigComponent.Initialize(rig);
            string controllerPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/PoseSamplerOwnerCurve.controller");
            AnimatorController animatorController =
                AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            animatorController.AddParameter("WeaponBoneWeight", AnimatorControllerParameterType.Float);
            animator.runtimeAnimatorController = animatorController;
            animator.Rebind();
            animator.Update(0f);
            animator.SetFloat("WeaponBoneWeight", 0.35f);
            var owner = new CharacterAnimInstance(
                new Pawn(root),
                new TestAnimationSource(root.transform),
                animator,
                rigComponent);
            try
            {
                var layerJob = new PoseSamplerLayerJob();
                SetPrivateField(layerJob, "settings", settings);
                SetPrivateField(layerJob, "owner", owner);
                AnimationScriptPlayable playable = AnimationScriptPlayable.Create(
                    graph,
                    new PoseSamplerJob());

                layerJob.UpdatePlayableJobData(playable, 0.8f);

                PoseSamplerJob job = playable.GetJobData<PoseSamplerJob>();
                Assert.That(job.Weight, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(job.WeaponBoneWeight, Is.EqualTo(0.35f).Within(0.00001f));
            }
            finally
            {
                owner.Dispose();
                graph.Destroy();
                AssetDatabase.DeleteAsset(controllerPath);
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void PoseOffsetJob_ProducesTheSourceOrderedAnimationStreamResult()
        {
            GameObject root = new GameObject("PoseOffsetParityRoot");
            Transform bone = new GameObject("Bone").transform;
            bone.SetParent(root.transform, false);
            Animator animator = root.AddComponent<Animator>();
            PlayableGraph graph = PlayableGraph.Create("PoseOffsetParity");
            NativeArray<TransformStreamHandle> handles = default;
            NativeArray<PoseOffsetJobData> poses = default;
            try
            {
                handles = new NativeArray<TransformStreamHandle>(1, Allocator.TempJob);
                handles[0] = animator.BindStreamTransform(bone);
                poses = new NativeArray<PoseOffsetJobData>(1, Allocator.TempJob);
                poses[0] = new PoseOffsetJobData(new KPose
                {
                    Pose = new KTransform(new Vector3(0.4f, -0.2f, 0.1f), Quaternion.Euler(0f, 30f, 0f)),
                    Space = TransformSpace.ParentBoneSpace,
                    ModifyMode = TransformModifyMode.Add
                });
                PoseOffsetJob job = new PoseOffsetJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Handles = handles,
                    Poses = poses,
                    Weight = 0.5f
                };
                AnimationScriptPlayable playable = AnimationScriptPlayable.Create(graph, job);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "PoseOffset", animator);
                output.SetSourcePlayable(playable);

                graph.Evaluate();

                Assert.That(Vector3.Distance(bone.localPosition, new Vector3(0.2f, -0.1f, 0.05f)), Is.LessThan(0.00001f));
                Assert.That(Quaternion.Angle(bone.localRotation, Quaternion.Euler(0f, 15f, 0f)), Is.LessThan(0.001f));
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (handles.IsCreated) handles.Dispose();
                if (poses.IsCreated) poses.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ViewJob_AppliesWeaponThenHandsInTheSourceAnimationStreamOrder()
        {
            GameObject root = new GameObject("ViewParityRoot");
            Transform weapon = CreateChild(root.transform, "Weapon");
            Transform rightHand = CreateChild(root.transform, "RightHand");
            Transform leftHand = CreateChild(root.transform, "LeftHand");
            Animator animator = root.AddComponent<Animator>();
            try
            {
                ViewJob job = new ViewJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Weapon = animator.BindStreamTransform(weapon),
                    RightHand = animator.BindStreamTransform(rightHand),
                    LeftHand = animator.BindStreamTransform(leftHand),
                    WeaponPose = ParentAdd(new Vector3(0.2f, 0f, 0f)),
                    RightHandPose = ParentAdd(new Vector3(0f, 0.3f, 0f)),
                    LeftHandPose = ParentAdd(new Vector3(0f, 0f, -0.4f)),
                    Weight = 0.5f
                };

                EvaluateJob(animator, job);

                AssertVector(weapon.localPosition, new Vector3(0.1f, 0f, 0f));
                AssertVector(rightHand.localPosition, new Vector3(0f, 0.15f, 0f));
                AssertVector(leftHand.localPosition, new Vector3(0f, 0f, -0.2f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ViewJob_AppliesWeaponOffsetInCharacterComponentSpace()
        {
            GameObject root = new GameObject("ViewComponentSpaceRoot");
            Transform weapon = CreateChild(root.transform, "Weapon");
            Transform rightHand = CreateChild(root.transform, "RightHand");
            Transform leftHand = CreateChild(root.transform, "LeftHand");
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Animator animator = root.AddComponent<Animator>();
            try
            {
                ViewJob job = new ViewJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Weapon = animator.BindStreamTransform(weapon),
                    RightHand = animator.BindStreamTransform(rightHand),
                    LeftHand = animator.BindStreamTransform(leftHand),
                    WeaponPose = new PoseOffsetJobData(new KPose
                    {
                        Pose = new KTransform(new Vector3(0f, 0f, 0.04f), Quaternion.identity),
                        Space = TransformSpace.ComponentSpace,
                        ModifyMode = TransformModifyMode.Add
                    }),
                    RightHandPose = ParentAdd(Vector3.zero),
                    LeftHandPose = ParentAdd(Vector3.zero),
                    Weight = 1f
                };

                EvaluateJob(animator, job);

                AssertVector(weapon.position, new Vector3(0.04f, 0f, 0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ViewSettings_RejectInvalidWeaponBone()
        {
            GameObject root = new GameObject("ViewSettingsValidationRoot");
            CreateChild(root.transform, "IK WeaponBone");
            CreateChild(root.transform, "IK RightHand");
            CreateChild(root.transform, "IK LeftHand");
            KRigComponent rigComponent = root.AddComponent<KRigComponent>();
            KRig rig = ScriptableObject.CreateInstance<KRig>();
            ViewLayerSettings settings = ScriptableObject.CreateInstance<ViewLayerSettings>();
            try
            {
                rigComponent.RefreshHierarchy();
                rig.Import(rigComponent);
                settings.Configure(rig);
                SetField(settings, "ikWeaponBone", new KPose
                {
                    Element = new KRigElement(-1, "Missing IK WeaponBone", 0),
                    Pose = KTransform.Identity,
                    Space = TransformSpace.ComponentSpace,
                    ModifyMode = TransformModifyMode.Add
                });

                Assert.That(() => settings.Validate(rig), Throws.InvalidOperationException);
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AttachHandJob_MatchesSourceWeaponRelativePoseAndFingerBlend()
        {
            GameObject root = new GameObject("AttachHandParityRoot");
            Transform weapon = CreateChild(root.transform, "IkWeapon");
            Transform hand = CreateChild(root.transform, "IkHand");
            Transform finger = CreateChild(hand, "Finger");
            weapon.position = new Vector3(1f, 2f, 3f);
            weapon.rotation = Quaternion.Euler(0f, 90f, 0f);
            Animator animator = root.AddComponent<Animator>();
            NativeArray<AttachHandPoseData> chain = default;
            try
            {
                chain = new NativeArray<AttachHandPoseData>(1, Allocator.TempJob);
                chain[0] = new AttachHandPoseData
                {
                    Handle = animator.BindStreamTransform(finger),
                    LocalRotation = Quaternion.Euler(30f, 0f, 0f)
                };
                AttachHandJob job = new AttachHandJob
                {
                    Hand = animator.BindStreamTransform(hand),
                    Weapon = animator.BindStreamTransform(weapon),
                    IkHand = animator.BindStreamTransform(hand),
                    IkWeapon = animator.BindStreamTransform(weapon),
                    RelativeHandPose = new KTransform(new Vector3(0.2f, 0f, 0f), Quaternion.identity),
                    HandPoseOffset = new KTransform(new Vector3(0f, 0.1f, 0f), Quaternion.Euler(0f, 0f, 20f)),
                    Chain = chain,
                    Weight = 0.5f,
                    ReferenceInitialized = true
                };

                EvaluateJob(animator, job);

                Vector3 attachedPosition = weapon.TransformPoint(new Vector3(0.2f, 0.1f, 0f));
                AssertVector(hand.position, Vector3.Lerp(Vector3.zero, attachedPosition, 0.5f));
                Assert.That(Quaternion.Angle(finger.localRotation, Quaternion.Euler(15f, 0f, 0f)), Is.LessThan(0.001f));
            }
            finally
            {
                if (chain.IsCreated) chain.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PoseSamplerJob_CopiesWeaponAndIkTargetsInsideOneAnimationStreamEvaluation()
        {
            GameObject root = new GameObject("PoseSamplerParityRoot");
            Transform pelvisParent = CreateChild(root.transform, "PelvisParent");
            Transform pelvis = CreateChild(pelvisParent, "Pelvis");
            Transform spine = CreateChild(pelvis, "Spine");
            Transform weapon = CreateChild(root.transform, "Weapon");
            Transform weaponRight = CreateChild(root.transform, "WeaponRight");
            Transform weaponLeft = CreateChild(root.transform, "WeaponLeft");
            Transform ikWeapon = CreateChild(root.transform, "IkWeapon");
            Transform rightHand = CreateChild(root.transform, "RightHandSource");
            Transform leftHand = CreateChild(root.transform, "LeftHandSource");
            Transform rightHint = CreateChild(root.transform, "RightHintSource");
            Transform leftHint = CreateChild(root.transform, "LeftHintSource");
            Transform ikRightHand = CreateChild(root.transform, "IkRightHand");
            Transform ikLeftHand = CreateChild(root.transform, "IkLeftHand");
            Transform ikRightHint = CreateChild(root.transform, "IkRightHint");
            Transform ikLeftHint = CreateChild(root.transform, "IkLeftHint");
            weaponRight.position = new Vector3(0.7f, 0.2f, -0.1f);
            rightHand.position = new Vector3(0.4f, 0.5f, 0.6f);
            leftHand.position = new Vector3(-0.4f, 0.5f, 0.6f);
            rightHint.position = new Vector3(0.3f, 0.8f, 0.1f);
            leftHint.position = new Vector3(-0.3f, 0.8f, 0.1f);
            Animator animator = root.AddComponent<Animator>();
            try
            {
                PoseSamplerJob job = new PoseSamplerJob
                {
                    CharacterRoot = animator.BindStreamTransform(root.transform),
                    SpineRoot = animator.BindStreamTransform(spine),
                    Pelvis = animator.BindStreamTransform(pelvis),
                    PelvisParent = animator.BindStreamTransform(pelvisParent),
                    WeaponBone = animator.BindStreamTransform(weapon),
                    WeaponBoneRight = animator.BindStreamTransform(weaponRight),
                    WeaponBoneLeft = animator.BindStreamTransform(weaponLeft),
                    IkWeaponBone = animator.BindStreamTransform(ikWeapon),
                    IkRightHand = animator.BindStreamTransform(ikRightHand),
                    IkLeftHand = animator.BindStreamTransform(ikLeftHand),
                    IkRightHandHint = animator.BindStreamTransform(ikRightHint),
                    IkLeftHandHint = animator.BindStreamTransform(ikLeftHint),
                    CachedPelvisPose = Quaternion.identity,
                    WeaponBoneOffset = KTransform.Identity,
                    WeaponBoneWeight = 0f,
                    StabilizationWeight = 0f,
                    Weight = 1f
                };
                // The source job reads the live hand and hint handles. Reuse its exact handle roles here.
                SetPublicHandle(ref job, "IkRightHand", animator.BindStreamTransform(rightHand));
                SetPublicHandle(ref job, "IkLeftHand", animator.BindStreamTransform(leftHand));
                SetPublicHandle(ref job, "IkRightHandHint", animator.BindStreamTransform(rightHint));
                SetPublicHandle(ref job, "IkLeftHandHint", animator.BindStreamTransform(leftHint));

                EvaluateJob(animator, job);

                AssertVector(ikWeapon.position, weaponRight.position);
                AssertVector(rightHand.position, new Vector3(0.4f, 0.5f, 0.6f));
                AssertVector(leftHand.position, new Vector3(-0.4f, 0.5f, 0.6f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PoseSamplerJob_ZeroWeightLeavesTheAnimationStreamUntouched()
        {
            GameObject root = new GameObject("PoseSamplerZeroWeightRoot");
            Transform observed = CreateChild(root.transform, "Observed");
            observed.localPosition = new Vector3(0.3f, -0.2f, 0.7f);
            observed.localRotation = Quaternion.Euler(12f, 23f, 34f);
            Animator animator = root.AddComponent<Animator>();
            try
            {
                PoseSamplerJob job = new PoseSamplerJob
                {
                    CharacterRoot = animator.BindStreamTransform(root.transform),
                    IkWeaponBone = animator.BindStreamTransform(observed),
                    Weight = 0f,
                    OverwriteRoot = true,
                    OverwriteWeaponBone = true,
                    WeaponBoneWeight = 1f,
                    StabilizationWeight = 1f
                };
                Vector3 position = observed.localPosition;
                Quaternion rotation = observed.localRotation;

                EvaluateJob(animator, job);

                AssertVector(observed.localPosition, position);
                Assert.That(
                    Quaternion.Angle(observed.localRotation, rotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IkJob_ReachesTheSameTwoBoneTargetAsTheSourceSolver()
        {
            GameObject root = new GameObject("IkParityRoot");
            Transform upper = CreateChild(root.transform, "Upper");
            Transform lower = CreateChild(upper, "Lower");
            Transform tip = CreateChild(lower, "Tip");
            lower.localPosition = Vector3.right;
            tip.localPosition = Vector3.right;
            Transform target = CreateChild(root.transform, "Target");
            target.position = new Vector3(1.2f, 0.8f, 0f);
            Transform hint = CreateChild(root.transform, "Hint");
            hint.position = new Vector3(0f, 0f, 1f);
            Animator animator = root.AddComponent<Animator>();
            try
            {
                IkJob job = new IkJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    RightHand = new IkHandle(animator, tip, target, hint),
                    Weight = 1f,
                    RightHandWeight = 1f
                };

                EvaluateJob(animator, job);

                Assert.That(Vector3.Distance(tip.position, target.position), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertJob<TSettings, TJob>()
            where TSettings : AnimationLayerSettings
            where TJob : IAnimationLayerJob
        {
            TSettings settings = ScriptableObject.CreateInstance<TSettings>();
            try
            {
                IAnimationLayerJob job = settings.CreateAnimationJob();
                Assert.That(job, Is.TypeOf<TJob>());
                Assert.That(job.SettingsType, Is.EqualTo(typeof(TSettings)));
                job.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        private static KTransform RandomTransform(System.Random random)
        {
            float Next(float minimum, float maximum)
            {
                return minimum + (float)random.NextDouble() * (maximum - minimum);
            }

            return new KTransform(
                new Vector3(Next(-2f, 2f), Next(-2f, 2f), Next(-2f, 2f)),
                Quaternion.Euler(Next(-170f, 170f), Next(-170f, 170f), Next(-170f, 170f)),
                new Vector3(Next(0.5f, 2f), Next(0.5f, 2f), Next(0.5f, 2f)));
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
        }

        private static PoseOffsetJobData ParentAdd(Vector3 position)
        {
            return new PoseOffsetJobData(new KPose
            {
                Pose = new KTransform(position, Quaternion.identity),
                Space = TransformSpace.ParentBoneSpace,
                ModifyMode = TransformModifyMode.Add
            });
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void EvaluateJob<T>(Animator animator, T job) where T : struct, IAnimationJob
        {
            PlayableGraph graph = PlayableGraph.Create(typeof(T).Name + "Parity");
            try
            {
                AnimationScriptPlayable playable = AnimationScriptPlayable.Create(graph, job);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, typeof(T).Name, animator);
                output.SetSourcePlayable(playable);
                graph.Evaluate();
            }
            finally
            {
                graph.Destroy();
            }
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));
        }

        private static void SetPublicHandle(ref PoseSamplerJob job, string fieldName, TransformStreamHandle handle)
        {
            object boxed = job;
            typeof(PoseSamplerJob).GetField(fieldName).SetValue(boxed, handle);
            job = (PoseSamplerJob)boxed;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
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

        private static void AssertPose(KTransform actual, KTransform expected)
        {
            Assert.That(Vector3.Distance(actual.Position, expected.Position), Is.LessThan(0.00001f));
            Assert.That(Quaternion.Angle(actual.Rotation, expected.Rotation), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(actual.Scale, expected.Scale), Is.LessThan(0.00001f));
        }
    }
}
