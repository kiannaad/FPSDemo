using System;
using System.Collections;
using CGame.Animation.Rig;
using Unity.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.Animation.Tests
{
    public sealed class ProceduralLayerLabPlayModeTests
    {
        private const string ResourceRoot = "ProceduralLayerLab/";

        [UnityTest]
        public IEnumerator LabPrefab_RunsHumanoidViewPassthroughAndSurvivesRebuildDispose()
        {
            GameObject prefab = Resources.Load<GameObject>(
                ResourceRoot + "ProceduralLayerLabCharacter");
            KRig rig = Resources.Load<KRig>(ResourceRoot + "ProceduralLayerLabRig");
            BoneProfile profile = Resources.Load<BoneProfile>(
                ResourceRoot + "ProceduralLayerLabProfile");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);

            Scene scene = SceneManager.CreateScene("ProceduralLayerLabRuntime");
            GameObject root = UnityEngine.Object.Instantiate(prefab);
            root.name = prefab.name;
            SceneManager.MoveGameObjectToScene(root, scene);
            CharacterAnimInstance animation = null;
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                KRigComponent rigComponent = root.GetComponentInChildren<KRigComponent>(true);
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.avatar, Is.Not.Null);
                Assert.That(animator.isHuman, Is.True);
                Assert.That(rigComponent, Is.Not.Null);
                Assert.That(profile.Layers, Has.Count.EqualTo(1));
                Assert.That(profile.Layers[0], Is.TypeOf<ViewLayerSettings>());

                rigComponent.Initialize(rig);
                animator.Rebind();
                animator.Update(0f);
                yield return null;

                animation = new CharacterAnimInstance(
                    new Pawn(root),
                    new TestAnimationSource(root.transform),
                    animator,
                    rigComponent);
                animation.BoneController.LinkProfile(profile);
                Assert.That(animation.TryRebuildGraph(), Is.True);
                Assert.That(animation.BoneController.ActiveProfile, Is.SameAs(profile));

                Transform weaponBone = FindTransform(root.transform, "IK WeaponBone");
                animation.UpdateAnimation(1f / 60f);
                animator.playableGraph.Evaluate(1f / 60f);
                animation.DispatchAnimationNotifies();
                Vector3 stablePosition = weaponBone.localPosition;
                Quaternion stableRotation = weaponBone.localRotation;

                for (int iteration = 0; iteration < 8; iteration++)
                {
                    animator.playableGraph.Evaluate(0f);
                    animation.DispatchAnimationNotifies();
                    Assert.That(weaponBone.localPosition, Is.EqualTo(stablePosition));
                    Assert.That(
                        Quaternion.Angle(weaponBone.localRotation, stableRotation),
                        Is.LessThan(0.0001f));
                }

                animation.BoneController.ReleaseOutput();
                Assert.That(animation.TryRebuildGraph(), Is.True);
                Assert.That(animation.BoneController.ActiveProfile, Is.SameAs(profile));
                animation.UpdateAnimation(1f / 60f);
                animator.playableGraph.Evaluate(1f / 60f);
                Assert.That(animator.playableGraph.IsValid(), Is.True);

                Assert.That(() => animation.Dispose(), Throws.Nothing);
                Assert.That(() => animation.Dispose(), Throws.Nothing);
                animation = null;
            }
            finally
            {
                animation?.Dispose();
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        [UnityTest]
        public IEnumerator BlendingJob_BlendsPositionAndRotationAndSurvivesGraphRebuild()
        {
            GameObject root = new GameObject("BlendingLabRoot");
            Transform bone = new GameObject("BlendBone").transform;
            bone.SetParent(root.transform, false);
            Animator animator = root.AddComponent<Animator>();
            NativeArray<BlendingJobAtom> elements = default;
            try
            {
                elements = new NativeArray<BlendingJobAtom>(1, Allocator.Persistent);
                elements[0] = new BlendingJobAtom
                {
                    Handle = animator.BindStreamTransform(bone),
                    ActivePose = KTransform.Identity,
                    DesiredComponentPose = new KTransform(
                        new Vector3(2f, 0f, 0f),
                        Quaternion.Euler(0f, 90f, 0f)),
                    ElementWeight = 0.5f
                };
                BlendingJob job = new BlendingJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Elements = elements,
                    Weight = 0.5f,
                    BlendPosition = true
                };

                EvaluateBlendingGraph(animator, job);
                Assert.That(Vector3.Distance(bone.position, new Vector3(0.5f, 0f, 0f)), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(bone.rotation, Quaternion.Euler(0f, 22.5f, 0f)), Is.LessThan(0.01f));

                bone.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                EvaluateBlendingGraph(animator, job);
                Assert.That(Vector3.Distance(bone.position, new Vector3(0.5f, 0f, 0f)), Is.LessThan(0.0001f));

                job.Weight = 0f;
                bone.SetPositionAndRotation(new Vector3(0.3f, 0.2f, -0.1f), Quaternion.Euler(1f, 2f, 3f));
                Vector3 zeroWeightPosition = bone.position;
                Quaternion zeroWeightRotation = bone.rotation;
                EvaluateBlendingGraph(animator, job);
                Assert.That(Vector3.Distance(bone.position, zeroWeightPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(bone.rotation, zeroWeightRotation), Is.LessThan(0.001f));
                yield return null;
            }
            finally
            {
                if (elements.IsCreated) elements.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void EvaluateBlendingGraph(Animator animator, BlendingJob job)
        {
            PlayableGraph graph = PlayableGraph.Create("BlendingLayerLab");
            try
            {
                AnimationScriptPlayable playable = AnimationScriptPlayable.Create(graph, job, 1);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Blending", animator);
                output.SetSourcePlayable(playable);
                graph.Evaluate();
            }
            finally
            {
                graph.Destroy();
            }
        }

        [UnityTest]
        public IEnumerator ViewMotionJobs_ApplyLookTurnAndPreparedSwayData()
        {
            GameObject root = new GameObject("ViewMotionLabRoot");
            Transform lookBone = CreateChild(root.transform, "LookBone");
            Transform pitchBone = CreateChild(root.transform, "PitchBone");
            Transform modelRoot = CreateChild(root.transform, "ModelRoot");
            Transform hip = CreateChild(modelRoot, "Hip");
            Transform upperBodyRoot = CreateChild(modelRoot, "UpperBodyRoot");
            Transform cameraAnchor = CreateChild(upperBodyRoot, "CameraAnchor");
            upperBodyRoot.localPosition = new Vector3(0.2f, 1.3f, 0.1f);
            cameraAnchor.localPosition = new Vector3(0f, 0.4f, 0.2f);
            Transform weapon = CreateChild(root.transform, "IK Weapon");
            Transform rightElbow = CreateChild(root.transform, "IK RightElbow");
            Transform leftElbow = CreateChild(root.transform, "IK LeftElbow");
            Transform head = CreateChild(root.transform, "Head");
            head.localPosition = new Vector3(0f, 1.6f, 0f);
            weapon.localPosition = new Vector3(0.2f, 1.2f, 0.4f);
            Animator animator = root.AddComponent<Animator>();
            KRigComponent rigComponent = root.AddComponent<KRigComponent>();
            KRig rig = ScriptableObject.CreateInstance<KRig>();
            SwayLayerSettings swaySettings = ScriptableObject.CreateInstance<SwayLayerSettings>();
            NativeArray<LookJobAtom> yaw = default;
            NativeArray<LookJobAtom> pitch = default;
            CharacterAnimInstance owner = null;
            try
            {
                rigComponent.RefreshHierarchy();
                rig.Import(rigComponent);
                rigComponent.Initialize(rig);
                SetPrivateField(swaySettings, "headBone", FindRigElement(rig, "Head"));
                swaySettings.Configure(rig);
                owner = new CharacterAnimInstance(
                    new Pawn(root),
                    new TestAnimationSource(root.transform),
                    animator,
                    rigComponent);
                WeaponLayerJobData weaponData = new WeaponLayerJobData();
                weaponData.Initialize(
                    new LayerJobData(animator, rigComponent, animator.BindStreamTransform(root.transform), owner),
                    swaySettings);
                owner.Dispose();
                owner = null;

                yaw = new NativeArray<LookJobAtom>(1, Allocator.Persistent);
                yaw[0] = new LookJobAtom
                {
                    Handle = animator.BindStreamTransform(lookBone),
                    AngleLimits = new Vector2(90f, 90f)
                };
                pitch = new NativeArray<LookJobAtom>(1, Allocator.Persistent);
                pitch[0] = new LookJobAtom
                {
                    Handle = animator.BindStreamTransform(pitchBone),
                    AngleLimits = new Vector2(90f, 90f)
                };
                LookJob lookJob = new LookJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Yaw = yaw,
                    Pitch = pitch,
                    Roll = new NativeArray<LookJobAtom>(0, Allocator.Persistent),
                    ViewAnglesDegrees = new Vector2(45f, 30f),
                    Weight = 1f
                };
                EvaluateJob(animator, lookJob);
                Assert.That(Quaternion.Angle(lookBone.rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.01f));
                Quaternion expectedPitch = Quaternion.AngleAxis(45f, Vector3.up)
                    * Quaternion.AngleAxis(30f, Vector3.right)
                    * Quaternion.Inverse(Quaternion.AngleAxis(45f, Vector3.up));
                Assert.That(Quaternion.Angle(pitchBone.rotation, expectedPitch), Is.LessThan(0.01f),
                    "Pitch must rotate around the right axis after Look yaw, not the initial root X axis.");
                lookJob.Pitch.Dispose();
                lookJob.Roll.Dispose();

                TurnJob turnJob = new TurnJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    ModelRoot = animator.BindStreamTransform(modelRoot),
                    TurnAngleDegrees = 30f,
                    Weight = 1f,
                    OffsetPosition = modelRoot != hip
                };
                root.transform.rotation = Quaternion.Euler(20f, 0f, 15f);
                Quaternion modelRotationBeforeTurn = modelRoot.rotation;
                Vector3 cameraBeforeTurn = cameraAnchor.position;
                EvaluateJob(animator, turnJob);
                Quaternion expectedWorldYaw = Quaternion.AngleAxis(30f, Vector3.up) * modelRotationBeforeTurn;
                Assert.That(Quaternion.Angle(modelRoot.rotation, expectedWorldYaw), Is.LessThan(0.01f),
                    "Turn yaw must use world up even when the animation root is pitched or rolled.");
                Assert.That(Quaternion.Angle(hip.rotation, modelRoot.rotation), Is.LessThan(0.01f),
                    "Hips and feet must inherit ModelRoot's counter-rotation instead of following the physical root.");
                Assert.That(Vector3.Distance(cameraAnchor.position, cameraBeforeTurn), Is.GreaterThan(0.0001f),
                    "UpperBodyRoot must naturally inherit the ModelRoot turn; Look owns its aim correction.");
                Assert.That(Quaternion.Angle(cameraAnchor.rotation, expectedWorldYaw), Is.LessThan(0.01f),
                    "Turn must not restore UpperBodyRoot's pre-turn world rotation.");

                SwayJob swayJob = new SwayJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Weapon = animator.BindStreamTransform(weapon),
                    Head = animator.BindStreamTransform(head),
                    WeaponData = weaponData,
                    FreeAimAngles = new Vector2(10f, 0f),
                    MovePose = KTransform.Identity,
                    AimPose = KTransform.Identity,
                    FreeAimSpace = TransformSpace.ComponentSpace,
                    MoveSpace = TransformSpace.ComponentSpace,
                    AimSpace = TransformSpace.ComponentSpace,
                    Weight = 1f
                };
                Quaternion weaponBefore = weapon.rotation;
                EvaluateJob(animator, swayJob);
                Assert.That(Quaternion.Angle(weapon.rotation, weaponBefore), Is.GreaterThan(5f));
                Assert.That(rightElbow, Is.Not.Null);
                Assert.That(leftElbow, Is.Not.Null);
                yield return null;
            }
            finally
            {
                owner?.Dispose();
                if (yaw.IsCreated) yaw.Dispose();
                UnityEngine.Object.Destroy(swaySettings);
                UnityEngine.Object.Destroy(rig);
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator AdsAndAdditiveJobs_ApplyPreparedAimAndRecoilData()
        {
            GameObject root = new GameObject("AdsAdditiveLabRoot");
            Transform weapon = CreateChild(root.transform, "IK Weapon");
            Transform rightElbow = CreateChild(root.transform, "IK RightElbow");
            Transform leftElbow = CreateChild(root.transform, "IK LeftElbow");
            Transform aimTarget = CreateChild(root.transform, "AimTarget");
            Transform additiveBone = CreateChild(root.transform, "AdditiveBone");
            weapon.localPosition = new Vector3(0.3f, 1f, 0.4f);
            aimTarget.localPosition = new Vector3(0f, 1f, 0.4f);
            additiveBone.localPosition = new Vector3(0.1f, 0f, 0f);
            Animator animator = root.AddComponent<Animator>();
            KRigComponent rigComponent = root.AddComponent<KRigComponent>();
            KRig rig = ScriptableObject.CreateInstance<KRig>();
            AdsLayerSettings settings = ScriptableObject.CreateInstance<AdsLayerSettings>();
            CharacterAnimInstance owner = null;
            try
            {
                rigComponent.RefreshHierarchy();
                rig.Import(rigComponent);
                rigComponent.Initialize(rig);
                SetPrivateField(settings, "aimTargetBone", FindRigElement(rig, "AimTarget"));
                settings.Configure(rig);
                owner = new CharacterAnimInstance(
                    new Pawn(root),
                    new TestAnimationSource(root.transform),
                    animator,
                    rigComponent);
                WeaponLayerJobData weaponData = new WeaponLayerJobData();
                weaponData.Initialize(
                    new LayerJobData(animator, rigComponent, animator.BindStreamTransform(root.transform), owner),
                    settings);
                owner.Dispose();
                owner = null;

                AdsJob ads = new AdsJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Weapon = animator.BindStreamTransform(weapon),
                    AimTarget = animator.BindStreamTransform(aimTarget),
                    WeaponData = weaponData,
                    AimPointOffset = KTransform.Identity,
                    PositionBlend = Vector3.zero,
                    RotationBlend = Vector3.zero,
                    AimingWeight = 1f,
                    CameraBlend = 0f,
                    AimingEase = EaseMode.Linear,
                    Weight = 1f
                };
                EvaluateJob(animator, ads);
                Assert.That(Mathf.Abs(weapon.position.x), Is.LessThan(0.001f));

                weapon.localPosition = new Vector3(0.3f, 1f, 0.4f);
                AdditiveJob additive = new AdditiveJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Weapon = animator.BindStreamTransform(weapon),
                    AdditiveBone = animator.BindStreamTransform(additiveBone),
                    WeaponData = weaponData,
                    RecoilOffset = new KTransform(new Vector3(0f, 0f, 0.05f), Quaternion.Euler(-5f, 0f, 0f)),
                    CurveScale = 1f,
                    Weight = 1f
                };
                Vector3 before = weapon.position;
                EvaluateJob(animator, additive);
                Assert.That(Vector3.Distance(weapon.position, before), Is.GreaterThan(0.05f));
                Assert.That(Quaternion.Angle(weapon.rotation, Quaternion.identity), Is.GreaterThan(1f));
                Assert.That(rightElbow, Is.Not.Null);
                Assert.That(leftElbow, Is.Not.Null);
                yield return null;
            }
            finally
            {
                owner?.Dispose();
                UnityEngine.Object.Destroy(settings);
                UnityEngine.Object.Destroy(rig);
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator IkMotionJob_AppliesTrajectoryAndSurvivesGraphRebuild()
        {
            GameObject root = new GameObject("IkMotionLabRoot");
            Transform target = CreateChild(root.transform, "IkMotionTarget");
            Animator animator = root.AddComponent<Animator>();
            try
            {
                IkMotionJob job = new IkMotionJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Target = animator.BindStreamTransform(target),
                    Motion = new KTransform(new Vector3(0.2f, -0.1f, 0.3f), Quaternion.Euler(10f, 20f, 30f)),
                    Weight = 0.5f
                };
                EvaluateJob(animator, job);
                Assert.That(Vector3.Distance(target.position, new Vector3(0.1f, -0.05f, 0.15f)), Is.LessThan(0.0001f));

                target.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                EvaluateJob(animator, job);
                Assert.That(Vector3.Distance(target.position, new Vector3(0.1f, -0.05f, 0.15f)), Is.LessThan(0.0001f));

                job.Weight = 0f;
                target.SetPositionAndRotation(new Vector3(0.4f, 0.5f, 0.6f), Quaternion.identity);
                Vector3 before = target.position;
                EvaluateJob(animator, job);
                Assert.That(Vector3.Distance(target.position, before), Is.LessThan(0.0001f));
                yield return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator CollisionJob_OutputsProbeBeforeApplyingNextFrameBlockingPose()
        {
            GameObject root = new GameObject("CollisionLabRoot");
            Transform weapon = CreateChild(root.transform, "CollisionWeapon");
            weapon.position = new Vector3(1f, 2f, 3f);
            weapon.rotation = Quaternion.Euler(0f, 90f, 0f);
            Animator animator = root.AddComponent<Animator>();
            PlayableGraph graph = PlayableGraph.Create("CollisionProbeLab");
            try
            {
                CollisionJob job = new CollisionJob
                {
                    Root = animator.BindStreamTransform(root.transform),
                    Weapon = animator.BindStreamTransform(weapon),
                    BlockingPose = KTransform.Identity,
                    TargetSpace = TransformSpace.ComponentSpace,
                    RayStartOffset = 0.2f,
                    Weight = 0f
                };
                AnimationScriptPlayable playable = AnimationScriptPlayable.Create(graph, job, 1);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Collision", animator);
                output.SetSourcePlayable(playable);
                graph.Evaluate();
                CollisionJob probeFrame = playable.GetJobData<CollisionJob>();
                Assert.That(Vector3.Distance(probeFrame.ProbeDirection, Vector3.right), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(probeFrame.ProbeOrigin, new Vector3(0.8f, 2f, 3f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(weapon.position, new Vector3(1f, 2f, 3f)), Is.LessThan(0.0001f));

                probeFrame.BlockingPose = new KTransform(new Vector3(0f, 0f, -0.4f), Quaternion.identity);
                probeFrame.Weight = 1f;
                playable.SetJobData(probeFrame);
                graph.Evaluate();
                Assert.That(Vector3.Distance(weapon.position, new Vector3(1f, 2f, 2.6f)), Is.LessThan(0.0001f));
                yield return null;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void EvaluateJob<T>(Animator animator, T job) where T : struct, IAnimationJob
        {
            PlayableGraph graph = PlayableGraph.Create(typeof(T).Name + "Lab");
            try
            {
                AnimationScriptPlayable playable = AnimationScriptPlayable.Create(graph, job, 1);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, typeof(T).Name, animator);
                output.SetSourcePlayable(playable);
                graph.Evaluate();
            }
            finally
            {
                graph.Destroy();
            }
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static KRigElement FindRigElement(KRig rig, string name)
        {
            foreach (KRigElement element in rig.Hierarchy)
            {
                if (element.Name == name) return element;
            }
            throw new InvalidOperationException("Missing test rig element " + name);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == name)
                {
                    return transforms[index];
                }
            }

            throw new InvalidOperationException("Missing procedural lab Transform: " + name);
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
