using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class CharacterBoneController : IDisposable
    {
        private readonly Animator animator;
        private readonly KRigComponent rigComponent;
        private readonly AnimationUpdateContext updateContext;
        private readonly TurnPresentationState turnState = new TurnPresentationState();
        private NativeArray<VirtualElementHandle> virtualElementHandles;
        private NativeArray<TransformStreamPose> blendingPoses;
        private NativeArray<int> cacheCompletionMarker;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationScriptPlayable virtualElementPlayable;
        private AnimationScriptPlayable blendingPlayable;
        private ProfileRuntime activeRuntime;
        private BoneProfile activeProfile;
        private BoneProfile nextProfile;
        private bool shouldLinkProfile;
        private bool isDisposed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int ProfileSwitchProbeFrameCount = 24;
        private const int RotationProbeFrameCount = 16;
        private const int LayerPoseProbeStageCount = 11;
        private const int LayerPoseProbeBoneCount = 5;
        private float diagnosticElapsed;
        private int profileSwitchProbeFramesRemaining;
        private NativeArray<Quaternion> layerPoseProbeRotations;
        private NativeArray<Quaternion> layerPoseProbeLocalRotations;
        private NativeArray<Vector3> layerPoseProbePositions;
        private NativeArray<int> layerPoseProbeMarkers;
        private AnimationScriptPlayable inputPoseProbePlayable;
        private AnimationScriptPlayable finalPoseProbePlayable;
        private bool firstTurnProcessProbePending;
        private int rotationProbeFramesRemaining;
        private bool rotationProbeArmed = true;
#endif

        public CharacterBoneController(
            Animator animator,
            KRigComponent rigComponent,
            CharacterAnimInstance owner)
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
            this.rigComponent = rigComponent ?? throw new ArgumentNullException(nameof(rigComponent));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            updateContext = owner.UpdateContext;
        }

        public CharacterAnimInstance Owner { get; }

        public TurnPresentationState TurnState => turnState;

        public bool TryPlayWeaponIkMotion(IkMotionLayerSettings motion)
        {
            return !isDisposed && IsValid() && activeRuntime != null && motion != null
                && activeProfile != null && motion.Rig == activeProfile.Rig
                && activeRuntime.TryPlayWeaponIkMotion(motion);
        }

        public bool IsWeaponIkMotionComplete(IkMotionLayerSettings motion)
        {
            return !isDisposed && IsValid() && activeRuntime != null && motion != null
                && activeProfile != null && motion.Rig == activeProfile.Rig
                && activeRuntime.IsWeaponIkMotionComplete(motion);
        }

        public bool HasWeaponIkMotionReachedEnd(IkMotionLayerSettings motion)
        {
            return !isDisposed && IsValid() && activeRuntime != null && motion != null
                && activeProfile != null && motion.Rig == activeProfile.Rig
                && activeRuntime.HasWeaponIkMotionReachedEnd(motion);
        }
public BoneProfile ActiveProfile => activeProfile;

        public bool TryGetWeaponCollisionProbe(
            out Vector3 origin,
            out Vector3 direction,
            out float distance)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;
            distance = 0f;
            return activeRuntime != null
                && activeRuntime.TryGetWeaponCollisionProbe(out origin, out direction, out distance);
        }

        public bool IsValid()
        {
            return !isDisposed && output.IsOutputValid();
        }

        public bool TryRebuild()
        {
            ThrowIfDisposed();
            if (IsValid())
            {
                return true;
            }

            PlayableGraph targetGraph = animator.playableGraph;
            if (!targetGraph.IsValid())
            {
                return false;
            }

            ReleaseOutput();
            try
            {
                graph = targetGraph;
                CreateBasePipeline();
                output = AnimationPlayableOutput.Create(graph, nameof(CharacterBoneController), animator);
                output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                output.SetSourcePlayable(finalPoseProbePlayable);
#else
                output.SetSourcePlayable(blendingPlayable);
#endif

                if (shouldLinkProfile)
                {
                    RebuildRequestedProfileWithoutBlend();
                }

                return IsValid();
            }
            catch
            {
                ReleaseOutput();
                throw;
            }
        }

        public void LinkProfile(BoneProfile profile)
        {
            ThrowIfDisposed();
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            profile.Validate(rigComponent.Rig);
            nextProfile = profile;
            shouldLinkProfile = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            profileSwitchProbeFramesRemaining = ProfileSwitchProbeFrameCount;
            WriteProfileSwitchProbe("link-request", activeProfile, profile);
#endif
            if (IsValid())
            {
                RequestPoseCache();
            }
        }

        public void UnlinkProfile()
        {
            ThrowIfDisposed();
            nextProfile = null;
            shouldLinkProfile = activeProfile != null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            profileSwitchProbeFramesRemaining = ProfileSwitchProbeFrameCount;
            WriteProfileSwitchProbe("unlink-request", activeProfile, null);
#endif
            if (shouldLinkProfile && IsValid())
            {
                RequestPoseCache();
            }
        }

        public void Update(float deltaTime)
        {
            if (!IsValid())
            {
                return;
            }

            UpdateRuntime(activeRuntime, deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ClearLayerPoseProbeMarkers();
            if (profileSwitchProbeFramesRemaining > 0)
            {
                WriteProfileSwitchProbe("pre-animation", activeProfile, nextProfile);
                profileSwitchProbeFramesRemaining--;
            }
            UpdateRotationProbeRequest();
            LogMotionDiagnostics(deltaTime);
#endif
        }

        public void PostAnimationUpdate()
        {
            if (isDisposed || !IsValid())
            {
                return;
            }

            activeRuntime?.PostUpdate();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (profileSwitchProbeFramesRemaining > 0)
            {
                WriteLayerPoseProbe();
                WriteFirstTurnProcessProbe();
            }
            WriteRotationProbe();
#endif
            if (shouldLinkProfile && cacheCompletionMarker[0] != 0)
            {
                ApplyProfileRequest();
            }
        }

        public void ReleaseOutput()
        {
            BoneProfile preservedProfile = shouldLinkProfile ? nextProfile : activeProfile;
            bool preserveRequest = shouldLinkProfile || activeProfile != null;

            if (graph.IsValid() && output.IsOutputValid())
            {
                graph.DestroyOutput(output);
            }

            DisposeRuntime(activeRuntime);
            activeRuntime = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DestroyPlayable(finalPoseProbePlayable);
            DestroyPlayable(inputPoseProbePlayable);
#endif
            DestroyPlayable(blendingPlayable);
            DestroyPlayable(virtualElementPlayable);

            DisposeNativeArrays();
            activeProfile = null;
            nextProfile = preservedProfile;
            shouldLinkProfile = preserveRequest;

            output = AnimationPlayableOutput.Null;
            virtualElementPlayable = AnimationScriptPlayable.Null;
            blendingPlayable = AnimationScriptPlayable.Null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            inputPoseProbePlayable = AnimationScriptPlayable.Null;
            finalPoseProbePlayable = AnimationScriptPlayable.Null;
#endif
            graph = default;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            ReleaseOutput();
            activeProfile = null;
            nextProfile = null;
            shouldLinkProfile = false;
            isDisposed = true;
        }

        private void CreateBasePipeline()
        {
            virtualElementHandles = CreateVirtualElementHandles();
            virtualElementPlayable = AnimationScriptPlayable.Create(
                graph,
                new VirtualElementJob { Handles = virtualElementHandles },
                0);

            blendingPoses = CreateBlendingPoses();
            cacheCompletionMarker = new NativeArray<int>(1, Allocator.Persistent);
            blendingPlayable = AnimationScriptPlayable.Create(
                graph,
                new AnimationBlendingJob
                {
                    Poses = blendingPoses,
                    CacheCompletionMarker = cacheCompletionMarker,
                    EaseMode = EaseMode.Linear
                },
                1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            layerPoseProbeRotations = new NativeArray<Quaternion>(
                LayerPoseProbeStageCount * LayerPoseProbeBoneCount,
                Allocator.Persistent);
            layerPoseProbeLocalRotations = new NativeArray<Quaternion>(
                LayerPoseProbeStageCount * LayerPoseProbeBoneCount,
                Allocator.Persistent);
            layerPoseProbePositions = new NativeArray<Vector3>(
                LayerPoseProbeStageCount * LayerPoseProbeBoneCount,
                Allocator.Persistent);
            layerPoseProbeMarkers = new NativeArray<int>(LayerPoseProbeStageCount, Allocator.Persistent);
            inputPoseProbePlayable = CreateLayerPoseProbePlayable(0);
            inputPoseProbePlayable.ConnectInput(0, virtualElementPlayable, 0, 1f);
            finalPoseProbePlayable = CreateLayerPoseProbePlayable(10);
            finalPoseProbePlayable.ConnectInput(0, blendingPlayable, 0, 1f);
            blendingPlayable.ConnectInput(0, inputPoseProbePlayable, 0, 1f);
#else
            blendingPlayable.ConnectInput(0, virtualElementPlayable, 0, 1f);
#endif
        }

        private NativeArray<VirtualElementHandle> CreateVirtualElementHandles()
        {
            HashSet<Transform> rigTransforms = new HashSet<Transform>();
            for (int index = 0; index < rigComponent.Rig.Hierarchy.Count; index++)
            {
                rigTransforms.Add(rigComponent.GetRigTransform(index));
            }

            KVirtualElement[] virtualElements = rigComponent.GetComponentsInChildren<KVirtualElement>(true);
            HashSet<Transform> uniqueVirtualTransforms = new HashSet<Transform>();
            NativeArray<VirtualElementHandle> handles =
                new NativeArray<VirtualElementHandle>(virtualElements.Length, Allocator.Persistent);
            try
            {
                for (int index = 0; index < virtualElements.Length; index++)
                {
                    KVirtualElement virtualElement = virtualElements[index];
                    Transform virtualTransform = virtualElement.transform;
                    Transform targetTransform = virtualElement.TargetBone;
                    if (!uniqueVirtualTransforms.Add(virtualTransform))
                    {
                        throw new InvalidOperationException("KRig contains a duplicate virtual element Transform.");
                    }

                    if (targetTransform == null)
                    {
                        throw new InvalidOperationException($"Virtual element '{virtualElement.name}' has no target bone.");
                    }

                    if (targetTransform == virtualTransform)
                    {
                        throw new InvalidOperationException($"Virtual element '{virtualElement.name}' cannot target itself.");
                    }

                    if (!rigTransforms.Contains(virtualTransform) || !rigTransforms.Contains(targetTransform))
                    {
                        throw new InvalidOperationException(
                            $"Virtual element '{virtualElement.name}' references a Transform outside its KRig hierarchy.");
                    }

                    handles[index] = new VirtualElementHandle
                    {
                        TargetHandle = animator.BindStreamTransform(targetTransform),
                        VirtualHandle = animator.BindStreamTransform(virtualTransform)
                    };
                }

                return handles;
            }
            catch
            {
                handles.Dispose();
                throw;
            }
        }

        private NativeArray<TransformStreamPose> CreateBlendingPoses()
        {
            int count = rigComponent.Rig.Hierarchy.Count;
            NativeArray<TransformStreamPose> poses =
                new NativeArray<TransformStreamPose>(count, Allocator.Persistent);
            for (int index = 0; index < count; index++)
            {
                poses[index] = new TransformStreamPose
                {
                    Handle = animator.BindStreamTransform(rigComponent.GetRigTransform(index)),
                    LocalPosition = Vector3.zero,
                    LocalRotation = Quaternion.identity
                };
            }

            return poses;
        }

        private ProfileRuntime BuildRuntime(BoneProfile profile, TurnRuntimeState? inheritedTurnState = null)
        {
            profile.Validate(rigComponent.Rig);
            ProfileRuntime runtime = new ProfileRuntime();
            Playable previousLayerPlayable = Playable.Null;
            try
            {
                LayerJobData jobData = new LayerJobData(
                    animator,
                    rigComponent,
                    animator.BindStreamTransform(rigComponent.transform),
                    Owner);
                foreach (AnimationLayerSettings layerSettings in profile.Layers)
                {
                    IAnimationLayerJob job = layerSettings.CreateAnimationJob();
                    if (job == null)
                    {
                        throw new InvalidOperationException($"{layerSettings.name} did not create an animation layer job.");
                    }

                    AnimationScriptPlayable playable = AnimationScriptPlayable.Null;
                    try
                    {
                        job.Initialize(jobData, layerSettings);
                        if (inheritedTurnState.HasValue && job is TurnLayerJob turnLayer)
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            float initializedAngle = turnLayer.Angle;
#endif
                            turnLayer.RestoreRuntimeState(inheritedTurnState.Value);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log(
                                $"[TurnProfileTransfer] phase=restore; profile={profile.name}; "
                                + $"initializedAngle={initializedAngle:F2}; "
                                + $"restoredAngle={turnLayer.Angle:F2}; "
                                + $"restoredTurning={turnLayer.IsTurning}",
                                rigComponent);
#endif
                        }
                        playable = job.CreatePlayable(graph);
                        if (!playable.IsValid())
                        {
                            throw new InvalidOperationException($"{layerSettings.name} did not create a valid animation playable.");
                        }

                        if (previousLayerPlayable.IsValid())
                        {
                            playable.ConnectInput(0, previousLayerPlayable, 0, 1f);
                        }

                        runtime.Add(new AnimationLayer(graph, layerSettings, job, playable));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        int probeStage = ResolveLayerPoseProbeStage(layerSettings);
                        if (probeStage >= 0)
                        {
                            AnimationScriptPlayable probePlayable = CreateLayerPoseProbePlayable(probeStage);
                            probePlayable.ConnectInput(0, playable, 0, 1f);
                            runtime.AddDiagnosticPlayable(probePlayable);
                            previousLayerPlayable = probePlayable;
                        }
                        else
#endif
                        {
                            previousLayerPlayable = playable;
                        }
                    }
                    catch
                    {
                        if (graph.IsValid() && playable.IsValid())
                        {
                            graph.DestroyPlayable(playable);
                        }

                        job.Dispose();
                        throw;
                    }
                }

                runtime.SetFinalPlayable(previousLayerPlayable);
                return runtime;
            }
            catch
            {
                runtime.Dispose();
                throw;
            }
        }

        private void ApplyProfileRequest()
        {
            BoneProfile requestedProfile = nextProfile;
            ProfileRuntime previousRuntime = activeRuntime;
            BoneProfile previousProfile = activeProfile;
            TurnRuntimeState? inheritedTurnState = previousRuntime != null
                && previousRuntime.TryGetTurnRuntimeState(out TurnRuntimeState turnRuntimeState)
                ? turnRuntimeState
                : null;
            ProfileRuntime requestedRuntime = requestedProfile != null
                ? BuildRuntime(requestedProfile, inheritedTurnState)
                : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WriteProfileSwitchProbe("apply-before", previousProfile, requestedProfile);
#endif

            previousRuntime?.DisconnectSource();
            if (blendingPlayable.GetInput(0).IsValid())
            {
                blendingPlayable.DisconnectInput(0);
            }
            Playable profileSource = ResolveProfileSourcePlayable();
            requestedRuntime?.ConnectSource(profileSource);
            ConnectBlendingSource(
                requestedRuntime?.ResolveFinalPlayable(profileSource)
                ?? profileSource);

            float blendDuration = requestedProfile != null
                ? requestedProfile.BlendIn
                : previousProfile?.BlendOut ?? 0f;
            EaseMode easeMode = requestedProfile != null
                ? requestedProfile.EaseMode
                : previousProfile?.EaseMode ?? EaseMode.Linear;
            ConfigureBlend(blendDuration, easeMode);

            activeRuntime = requestedRuntime;
            activeProfile = requestedProfile;
            shouldLinkProfile = false;
            DisposeRuntime(previousRuntime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            profileSwitchProbeFramesRemaining = ProfileSwitchProbeFrameCount;
            firstTurnProcessProbePending = inheritedTurnState.HasValue && requestedRuntime != null;
            WriteProfileSwitchProbe("apply-after", previousProfile, activeProfile);
#endif
        }

        private void RebuildRequestedProfileWithoutBlend()
        {
            BoneProfile requestedProfile = nextProfile;
            ProfileRuntime requestedRuntime = requestedProfile != null
                ? BuildRuntime(requestedProfile)
                : null;
            if (blendingPlayable.GetInput(0).IsValid())
            {
                blendingPlayable.DisconnectInput(0);
            }

            Playable profileSource = ResolveProfileSourcePlayable();
            requestedRuntime?.ConnectSource(profileSource);
            ConnectBlendingSource(
                requestedRuntime?.ResolveFinalPlayable(profileSource)
                ?? profileSource);
            ConfigureBlend(0f, requestedProfile?.EaseMode ?? EaseMode.Linear);
            activeRuntime = requestedRuntime;
            activeProfile = requestedProfile;
            shouldLinkProfile = false;
        }

        private void RequestPoseCache()
        {
            cacheCompletionMarker[0] = 0;
            AnimationBlendingJob job = blendingPlayable.GetJobData<AnimationBlendingJob>();
            job.CacheRequested = true;
            job.IsBlending = false;
            blendingPlayable.SetJobData(job);
        }

        private void ConfigureBlend(float duration, EaseMode easeMode)
        {
            AnimationBlendingJob job = blendingPlayable.GetJobData<AnimationBlendingJob>();
            job.CacheRequested = false;
            job.BlendDuration = duration;
            job.EaseMode = easeMode;
            job.Playback = 0f;
            job.IsBlending = duration > 0f;
            blendingPlayable.SetJobData(job);
        }

        private void ConnectBlendingSource(Playable source)
        {
            if (blendingPlayable.GetInput(0).IsValid())
            {
                blendingPlayable.DisconnectInput(0);
            }

            blendingPlayable.ConnectInput(0, source, 0, 1f);
        }

        private Playable ResolveProfileSourcePlayable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return inputPoseProbePlayable;
#else
            return virtualElementPlayable;
#endif
        }

        private void UpdateRuntime(ProfileRuntime runtime, float deltaTime)
        {
            if (runtime == null)
            {
                return;
            }

            runtime.Update(Owner, deltaTime);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private AnimationScriptPlayable CreateLayerPoseProbePlayable(int stage)
        {
            return AnimationScriptPlayable.Create(
                graph,
                new LayerPoseProbeJob
                {
                    Rotations = layerPoseProbeRotations,
                    LocalRotations = layerPoseProbeLocalRotations,
                    Positions = layerPoseProbePositions,
                    Markers = layerPoseProbeMarkers,
                    Stage = stage,
                    ModelRoot = animator.BindStreamTransform(FindRigTransform("Skeleton")),
                    Spine = animator.BindStreamTransform(FindRigTransform("Spine")),
                    UpperChest = animator.BindStreamTransform(FindRigTransform("UpperChest")),
                    WeaponBone = animator.BindStreamTransform(FindRigTransform("WeaponBone")),
                    IkWeaponBone = animator.BindStreamTransform(FindRigTransform("IK WeaponBone"))
                },
                1);
        }

        private static int ResolveLayerPoseProbeStage(AnimationLayerSettings settings)
        {
            if (settings is PoseSamplerLayerSettings) return 1;
            if (settings is IkMotionLayerSettings) return 2;
            if (settings is AttachHandLayerSettings) return 3;
            if (settings is ViewLayerSettings) return 4;
            if (settings is AdsLayerSettings) return 5;
            if (settings is AdditiveLayerSettings) return 6;
            if (settings is LookLayerSettings) return 7;
            if (settings is TurnLayerSettings) return 8;
            if (settings is IkLayerSettings) return 9;
            return -1;
        }

        private void ClearLayerPoseProbeMarkers()
        {
            if (!layerPoseProbeMarkers.IsCreated)
            {
                return;
            }

            for (int index = 0; index < layerPoseProbeMarkers.Length; index++)
            {
                layerPoseProbeMarkers[index] = 0;
            }
        }

        private void WriteLayerPoseProbe()
        {
            if (!layerPoseProbeRotations.IsCreated
                || !layerPoseProbeLocalRotations.IsCreated
                || !layerPoseProbePositions.IsCreated
                || !layerPoseProbeMarkers.IsCreated)
            {
                return;
            }

            string[] stageNames =
            {
                "InputPose", "PoseSampler", "IkMotion", "AttachHand", "View", "Ads",
                "Additive", "Look", "Turn", "IK", "ProfileBlend"
            };
            string ikMotionState = activeRuntime?.DescribeIkMotionState() ?? "none";
            for (int stage = 0; stage < LayerPoseProbeStageCount; stage++)
            {
                if (layerPoseProbeMarkers[stage] == 0)
                {
                    continue;
                }

                int offset = stage * LayerPoseProbeBoneCount;
                Debug.Log(
                    $"[LayerFacingProbe] frame={Time.frameCount}; stage={stageNames[stage]}; "
                    + $"profile={(activeProfile != null ? activeProfile.name : "<none>")}; "
                    + $"modelRootYaw={ExtractYaw(layerPoseProbeRotations[offset]):F2}; "
                    + $"modelRootLocalYaw={ExtractYaw(layerPoseProbeLocalRotations[offset]):F2}; "
                    + $"spineYaw={ExtractYaw(layerPoseProbeRotations[offset + 1]):F2}; "
                    + $"spineLocalYaw={ExtractYaw(layerPoseProbeLocalRotations[offset + 1]):F2}; "
                    + $"upperChestYaw={ExtractYaw(layerPoseProbeRotations[offset + 2]):F2}; "
                    + $"upperChestLocalYaw={ExtractYaw(layerPoseProbeLocalRotations[offset + 2]):F2}; "
                    + $"weaponBoneYaw={ExtractYaw(layerPoseProbeRotations[offset + 3]):F2}; "
                    + $"weaponBoneLocalYaw={ExtractYaw(layerPoseProbeLocalRotations[offset + 3]):F2}; "
                    + $"ikWeaponBoneYaw={ExtractYaw(layerPoseProbeRotations[offset + 4]):F2}; "
                    + $"ikWeaponBoneLocalYaw={ExtractYaw(layerPoseProbeLocalRotations[offset + 4]):F2}; "
                    + $"controlYaw={NormalizeSignedYaw(updateContext.ControlRotation.eulerAngles.y):F2}; "
                    + $"ikMotion={ikMotionState}",
                    rigComponent);
            }
        }

        private void WriteFirstTurnProcessProbe()
        {
            if (!firstTurnProcessProbePending)
            {
                return;
            }

            firstTurnProcessProbePending = false;
            const int lookStage = 7;
            const int turnStage = 8;
            if (layerPoseProbeMarkers[lookStage] == 0 || layerPoseProbeMarkers[turnStage] == 0)
            {
                Debug.LogWarning("[TurnProcessProbe] first post-switch frame has no Look/Turn stream samples.", rigComponent);
                return;
            }

            int lookOffset = lookStage * LayerPoseProbeBoneCount;
            int turnOffset = turnStage * LayerPoseProbeBoneCount;
            float lookWorldYaw = ExtractYaw(layerPoseProbeRotations[lookOffset]);
            float turnWorldYaw = ExtractYaw(layerPoseProbeRotations[turnOffset]);
            float lookLocalYaw = ExtractYaw(layerPoseProbeLocalRotations[lookOffset]);
            float turnLocalYaw = ExtractYaw(layerPoseProbeLocalRotations[turnOffset]);
            float runtimeAngle = activeRuntime != null
                && activeRuntime.TryGetTurnRuntimeState(out TurnRuntimeState turnRuntimeState)
                ? turnRuntimeState.Angle
                : 0f;
            Debug.Log(
                $"[TurnProcessProbe] frame={Time.frameCount}; profile={activeProfile.name}; "
                + $"stateAngle={runtimeAngle:F2}; contextTurnOffset={updateContext.TurnOffsetDegrees:F2}; "
                + $"lookWorldYaw={lookWorldYaw:F2}; turnWorldYaw={turnWorldYaw:F2}; "
                + $"processWorldDelta={Mathf.DeltaAngle(lookWorldYaw, turnWorldYaw):F2}; "
                + $"lookLocalYaw={lookLocalYaw:F2}; turnLocalYaw={turnLocalYaw:F2}; "
                + $"processLocalDelta={Mathf.DeltaAngle(lookLocalYaw, turnLocalYaw):F2}",
                rigComponent);
        }

        private void UpdateRotationProbeRequest()
        {
            const float minimumViewDeltaDegrees = 0.1f;
            bool isRotating = Mathf.Abs(updateContext.ViewDeltaDegrees.x) >= minimumViewDeltaDegrees;
            if (!isRotating)
            {
                rotationProbeArmed = true;
                return;
            }

            if (!rotationProbeArmed)
            {
                return;
            }

            rotationProbeArmed = false;
            rotationProbeFramesRemaining = RotationProbeFrameCount;
        }

        private void WriteRotationProbe()
        {
            if (rotationProbeFramesRemaining <= 0)
            {
                return;
            }

            rotationProbeFramesRemaining--;
            const int inputStage = 0;
            const int poseSamplerStage = 1;
            const int lookStage = 7;
            const int turnStage = 8;
            const int ikStage = 9;
            if (layerPoseProbeMarkers[inputStage] == 0
                || layerPoseProbeMarkers[poseSamplerStage] == 0
                || layerPoseProbeMarkers[lookStage] == 0
                || layerPoseProbeMarkers[turnStage] == 0
                || layerPoseProbeMarkers[ikStage] == 0)
            {
                Debug.LogWarning("[TurnRotationProbe] missing InputPose, PoseSampler, Look, Turn, or IK stream sample.", rigComponent);
                return;
            }

            float runtimeAngle = activeRuntime != null
                && activeRuntime.TryGetTurnRuntimeState(out TurnRuntimeState turnRuntimeState)
                ? turnRuntimeState.Angle
                : 0f;
            bool isTurning = activeRuntime != null
                && activeRuntime.TryGetTurnRuntimeState(out turnRuntimeState)
                && turnRuntimeState.IsTurning;
            int inputOffset = inputStage * LayerPoseProbeBoneCount;
            int poseSamplerOffset = poseSamplerStage * LayerPoseProbeBoneCount;
            int lookOffset = lookStage * LayerPoseProbeBoneCount;
            int turnOffset = turnStage * LayerPoseProbeBoneCount;
            int ikOffset = ikStage * LayerPoseProbeBoneCount;
            float inputLocalYaw = ExtractYaw(layerPoseProbeLocalRotations[inputOffset]);
            float poseSamplerLocalYaw = ExtractYaw(layerPoseProbeLocalRotations[poseSamplerOffset]);
            float turnLocalYaw = ExtractYaw(layerPoseProbeLocalRotations[turnOffset]);
            float ikLocalYaw = ExtractYaw(layerPoseProbeLocalRotations[ikOffset]);
            Debug.Log(
                $"[TurnRotationProbe] frame={Time.frameCount}; "
                + $"viewDeltaYaw={updateContext.ViewDeltaDegrees.x:F2}; "
                + $"rootYaw={NormalizeSignedYaw(updateContext.RootRotation.eulerAngles.y):F2}; "
                + $"controlYaw={NormalizeSignedYaw(updateContext.ControlRotation.eulerAngles.y):F2}; "
                + $"moving={updateContext.CharacterState.IsMoving}; "
                + $"stateAngle={runtimeAngle:F2}; isTurning={isTurning}; "
                + $"inputLocalYaw={inputLocalYaw:F2}; "
                + $"poseSamplerLocalYaw={poseSamplerLocalYaw:F2}; "
                + $"turnLocalYaw={turnLocalYaw:F2}; "
                + $"ikLocalYaw={ikLocalYaw:F2}; "
                + $"poseSamplerDelta={Mathf.DeltaAngle(inputLocalYaw, poseSamplerLocalYaw):F2}; "
                + $"turnDelta={Mathf.DeltaAngle(poseSamplerLocalYaw, turnLocalYaw):F2}; "
                + DescribeProbeBoneDelta("spine", inputOffset + 1, poseSamplerOffset + 1, lookOffset + 1, turnOffset + 1, ikOffset + 1)
                + DescribeProbeBoneDelta("weapon", inputOffset + 3, poseSamplerOffset + 3, lookOffset + 3, turnOffset + 3, ikOffset + 3)
                + DescribeProbeBoneDelta("ikWeapon", inputOffset + 4, poseSamplerOffset + 4, lookOffset + 4, turnOffset + 4, ikOffset + 4),
                rigComponent);
        }

        private string DescribeProbeBoneDelta(
            string name,
            int inputIndex,
            int poseSamplerIndex,
            int lookIndex,
            int turnIndex,
            int ikIndex)
        {
            return $"{name}PoseYaw={Mathf.DeltaAngle(ExtractYaw(layerPoseProbeRotations[inputIndex]), ExtractYaw(layerPoseProbeRotations[poseSamplerIndex])):F2}; "
                + $"{name}LookYaw={Mathf.DeltaAngle(ExtractYaw(layerPoseProbeRotations[poseSamplerIndex]), ExtractYaw(layerPoseProbeRotations[lookIndex])):F2}; "
                + $"{name}TurnYaw={Mathf.DeltaAngle(ExtractYaw(layerPoseProbeRotations[lookIndex]), ExtractYaw(layerPoseProbeRotations[turnIndex])):F2}; "
                + $"{name}IkYaw={Mathf.DeltaAngle(ExtractYaw(layerPoseProbeRotations[turnIndex]), ExtractYaw(layerPoseProbeRotations[ikIndex])):F2}; "
                + $"{name}PosePos={(layerPoseProbePositions[poseSamplerIndex] - layerPoseProbePositions[inputIndex]):F4}; "
                + $"{name}LookPos={(layerPoseProbePositions[lookIndex] - layerPoseProbePositions[poseSamplerIndex]):F4}; "
                + $"{name}TurnPos={(layerPoseProbePositions[turnIndex] - layerPoseProbePositions[lookIndex]):F4}; "
                + $"{name}IkPos={(layerPoseProbePositions[ikIndex] - layerPoseProbePositions[turnIndex]):F4}; ";
        }

        private static float ExtractYaw(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 planarForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            return planarForward.sqrMagnitude > 0.000001f
                ? Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg
                : 0f;
        }

        private void WriteProfileSwitchProbe(
            string phase,
            BoneProfile fromProfile,
            BoneProfile toProfile)
        {
            float controlYaw = NormalizeSignedYaw(updateContext.ControlRotation.eulerAngles.y);
            string activeName = activeProfile != null ? activeProfile.name : "<none>";
            string fromName = fromProfile != null ? fromProfile.name : "<none>";
            string toName = toProfile != null ? toProfile.name : "<none>";
            Debug.Log(
                $"[ProfileFacingProbe] frame={Time.frameCount}; phase={phase}; "
                + $"active={activeName}; from={fromName}; to={toName}; "
                + $"activeLayers={DescribeLayers(activeProfile)}; "
                + $"hasLook={HasLayer<LookLayerSettings>(activeProfile)}; "
                + $"hasTurn={HasLayer<TurnLayerSettings>(activeProfile)}; "
                + $"controlYaw={controlYaw:F2}; "
                + $"viewYaw={updateContext.ViewAnglesDegrees.x:F2}; "
                + $"turnOffset={updateContext.TurnOffsetDegrees:F2}; "
                + $"linkPending={shouldLinkProfile}",
                rigComponent);
        }

        private static bool HasLayer<TLayer>(BoneProfile profile)
            where TLayer : AnimationLayerSettings
        {
            if (profile == null)
            {
                return false;
            }

            foreach (AnimationLayerSettings layer in profile.Layers)
            {
                if (layer is TLayer)
                {
                    return true;
                }
            }

            return false;
        }

        private static float NormalizeSignedYaw(float yaw)
        {
            return Mathf.DeltaAngle(0f, yaw);
        }

        private void LogMotionDiagnostics(float deltaTime)
        {
            if (activeProfile == null || !HasMotionDiagnosticsLayer(activeProfile))
            {
                return;
            }

            diagnosticElapsed += Mathf.Max(0f, deltaTime);
            if (diagnosticElapsed < 0.25f)
            {
                return;
            }

            diagnosticElapsed = 0f;
            Pawn pawn = updateContext.Pawn;
            if (pawn.Transform == null)
            {
                return;
            }

            Transform leftFoot = FindRigTransform("LeftFoot");
            Transform rightFoot = FindRigTransform("RightFoot");
            Transform weaponBone = FindRigTransform("WeaponBone");
            Transform ikWeaponBone = FindRigTransform("IK WeaponBone");
            Transform rightHand = FindRigTransform("RightHand");
            Transform ikRightHand = FindRigTransform("IK RightHand");
            Transform leftHand = FindRigTransform("LeftHand");
            Transform ikLeftHand = FindRigTransform("IK LeftHand");
            Camera camera = rigComponent.GetComponentInChildren<Camera>(true);
            AnimatorStateInfo animatorState = animator.GetCurrentAnimatorStateInfo(0);
            float rootRigAngle = Quaternion.Angle(pawn.Transform.rotation, rigComponent.transform.rotation);
            // Debug.Log(
            //     $"[ProceduralMotion] Profile={activeProfile.name}; Layers={DescribeLayers(activeProfile)}; "
            //     + $"Controller={animator.runtimeAnimatorController.name}; "
            //     + $"Presentation={pawn.PresentationRotation.eulerAngles}; Pawn={pawn.Transform.rotation.eulerAngles}; "
            //     + $"Rig={rigComponent.transform.rotation.eulerAngles}; PawnRigAngle={rootRigAngle:F3}; "
            //     + $"View={updateContext.ViewAnglesDegrees}; TurnOffset={updateContext.TurnOffsetDegrees:F3}; "
            //     + $"AnimatorHash={animatorState.fullPathHash}; AnimatorTime={animatorState.normalizedTime:F3}; "
            //     + $"LeftFoot={DescribePosition(leftFoot)}; RightFoot={DescribePosition(rightFoot)}; "
            //     + $"Weapon={DescribePosition(weaponBone)}; IkWeapon={DescribePosition(ikWeaponBone)}; "
            //     + $"Camera={DescribeTransform(camera?.transform)}; IkWeaponInCamera={DescribeRelativePose(camera?.transform, ikWeaponBone)}; "
            //     + $"RightHand={DescribePosition(rightHand)}; IkRightHand={DescribePosition(ikRightHand)}; "
            //     + $"LeftHand={DescribePosition(leftHand)}; IkLeftHand={DescribePosition(ikLeftHand)}",
            //     rigComponent);
        }

        private static bool HasMotionDiagnosticsLayer(BoneProfile profile)
        {
            foreach (AnimationLayerSettings layer in profile.Layers)
            {
                if (layer is LookLayerSettings || layer is TurnLayerSettings || layer is IkLayerSettings)
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeLayers(BoneProfile profile)
        {
            if (profile == null)
            {
                return "<none>";
            }

            string[] names = new string[profile.Layers.Count];
            for (int index = 0; index < profile.Layers.Count; index++)
            {
                AnimationLayerSettings layer = profile.Layers[index];
                names[index] = layer != null ? layer.GetType().Name : "<null>";
            }

            return string.Join(" -> ", names);
        }

        private static string DescribePosition(Transform transform)
        {
            return transform == null ? "n/a" : transform.position.ToString("F3");
        }
        private static string DescribeTransform(Transform transform)
        {
            return transform == null
                ? "n/a"
                : $"{transform.position:F3}/{transform.rotation.eulerAngles:F3}";
        }

        private static string DescribeRelativePose(Transform reference, Transform transform)
        {
            if (reference == null || transform == null) return "n/a";
            Vector3 position = reference.InverseTransformPoint(transform.position);
            Quaternion rotation = Quaternion.Inverse(reference.rotation) * transform.rotation;
            return $"{position:F3}/{rotation.eulerAngles:F3}";
        }


        private Transform FindRigTransform(string elementName)
        {
            for (int index = 0; index < rigComponent.Rig.Hierarchy.Count; index++)
            {
                if (string.Equals(rigComponent.Rig.Hierarchy[index].Name, elementName, StringComparison.Ordinal))
                {
                    return rigComponent.GetRigTransform(index);
                }
            }

            return null;
        }
#endif

        private void DestroyPlayable(AnimationScriptPlayable playable)
        {
            if (graph.IsValid() && playable.IsValid())
            {
                graph.DestroyPlayable(playable);
            }
        }

        private void DisposeNativeArrays()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (layerPoseProbeMarkers.IsCreated) layerPoseProbeMarkers.Dispose();
            if (layerPoseProbePositions.IsCreated) layerPoseProbePositions.Dispose();
            if (layerPoseProbeLocalRotations.IsCreated) layerPoseProbeLocalRotations.Dispose();
            if (layerPoseProbeRotations.IsCreated) layerPoseProbeRotations.Dispose();
#endif
            if (cacheCompletionMarker.IsCreated) cacheCompletionMarker.Dispose();
            if (blendingPoses.IsCreated) blendingPoses.Dispose();
            if (virtualElementHandles.IsCreated) virtualElementHandles.Dispose();
        }

        private static void DisposeRuntime(ProfileRuntime runtime)
        {
            runtime?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(CharacterBoneController));
            }
        }

        private sealed class ProfileRuntime : IDisposable
        {
            private readonly List<AnimationLayer> layers = new List<AnimationLayer>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            private readonly List<AnimationScriptPlayable> diagnosticPlayables =
                new List<AnimationScriptPlayable>();
#endif
            private Playable finalPlayable = Playable.Null;
            private bool isDisposed;



            public bool TryPlayWeaponIkMotion(IkMotionLayerSettings motion)
            {
                foreach (AnimationLayer layer in layers)
                {
                    if (layer.Job is IkMotionLayerJob job && job.TryPlay(motion)) return true;
                }
                return false;
            }

            public bool IsWeaponIkMotionComplete(IkMotionLayerSettings motion)
            {
                foreach (AnimationLayer layer in layers)
                {
                    if (layer.Job is IkMotionLayerJob job && job.IsCompleteFor(motion)) return true;
                }

                return false;
            }

            public bool HasWeaponIkMotionReachedEnd(IkMotionLayerSettings motion)
            {
                foreach (AnimationLayer layer in layers)
                {
                    if (layer.Job is IkMotionLayerJob job && job.HasReachedEndFor(motion)) return true;
                }

                return false;
            }

            public bool TryGetTurnRuntimeState(out TurnRuntimeState turnRuntimeState)
            {
                foreach (AnimationLayer layer in layers)
                {
                    if (layer.Job is TurnLayerJob turnLayer)
                    {
                        turnRuntimeState = turnLayer.CaptureRuntimeState();
                        return true;
                    }
                }

                turnRuntimeState = default;
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public string DescribeIkMotionState()
            {
                foreach (AnimationLayer layer in layers)
                {
                    if (layer.Job is IkMotionLayerJob job)
                    {
                        return $"name={job.ActiveMotionName}; playing={job.IsPlaying}; "
                            + $"blendingOut={job.IsBlendingOut}; complete={job.IsComplete}; "
                            + $"playback={job.Playback:F3}; rotation={job.CurrentMotion.Rotation.eulerAngles:F2}";
                    }
                }

                return "missing";
            }
#endif
public void Add(AnimationLayer layer)
            {
                layers.Add(layer ?? throw new ArgumentNullException(nameof(layer)));
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public void AddDiagnosticPlayable(AnimationScriptPlayable playable)
            {
                diagnosticPlayables.Add(playable);
            }
#endif

            public void SetFinalPlayable(Playable playable)
            {
                finalPlayable = playable;
            }

            public Playable ResolveFinalPlayable(Playable emptySource)
            {
                return finalPlayable.IsValid() ? finalPlayable : emptySource;
            }

            public void ConnectSource(Playable source)
            {
                if (layers.Count == 0)
                {
                    return;
                }

                AnimationScriptPlayable firstPlayable = layers[0].Playable;
                if (firstPlayable.GetInput(0).IsValid())
                {
                    firstPlayable.DisconnectInput(0);
                }

                firstPlayable.ConnectInput(0, source, 0, 1f);
            }

            public void DisconnectSource()
            {
                if (layers.Count > 0 && layers[0].Playable.GetInput(0).IsValid())
                {
                    layers[0].Playable.DisconnectInput(0);
                }
            }

            public void Update(CharacterAnimInstance owner, float deltaTime)
            {
                float[] weights = new float[layers.Count];
                for (int index = 0; index < layers.Count; index++)
                {
                    weights[index] = layers[index].Settings.EvaluateWeight(owner);
                }

                // Turn produces the shared offset consumed by Look. Its playable must remain after
                // Look so ModelRoot is rotated last, but its update must run first to avoid Look
                // caching the previous frame's offset.
                for (int index = 0; index < layers.Count; index++)
                {
                    if (layers[index].Job is TurnLayerJob)
                    {
                        layers[index].PreUpdate(deltaTime, weights[index]);
                    }
                }

                for (int index = 0; index < layers.Count; index++)
                {
                    if (layers[index].Job is not TurnLayerJob)
                    {
                        layers[index].PreUpdate(deltaTime, weights[index]);
                    }
                }

                for (int index = 0; index < layers.Count; index++)
                {
                    AnimationLayer layer = layers[index];
                    layer.UpdatePlayableJobData(weights[index]);
                }
            }

            public void PostUpdate()
            {
                for (int index = 0; index < layers.Count; index++)
                {
                    layers[index].PostUpdate();
                }
            }

            public bool TryGetWeaponCollisionProbe(
                out Vector3 origin,
                out Vector3 direction,
                out float distance)
            {
                for (int index = 0; index < layers.Count; index++)
                {
                    if (layers[index].Job is CollisionLayerJob collision
                        && collision.ProbeLength > 0f)
                    {
                        origin = collision.ProbeOrigin;
                        direction = collision.ProbeDirection;
                        distance = collision.ProbeLength;
                        return direction.sqrMagnitude > 0.0001f;
                    }
                }

                origin = Vector3.zero;
                direction = Vector3.forward;
                distance = 0f;
                return false;
            }

            public void Dispose()
            {
                if (isDisposed)
                {
                    return;
                }

                DisconnectSource();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                for (int index = diagnosticPlayables.Count - 1; index >= 0; index--)
                {
                    AnimationScriptPlayable playable = diagnosticPlayables[index];
                    if (playable.IsValid())
                    {
                        playable.GetGraph().DestroyPlayable(playable);
                    }
                }

                diagnosticPlayables.Clear();
#endif
                for (int index = layers.Count - 1; index >= 0; index--)
                {
                    layers[index].Dispose();
                }

                layers.Clear();
                isDisposed = true;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private struct LayerPoseProbeJob : IAnimationJob
        {
            public NativeArray<Quaternion> Rotations;
            public NativeArray<Quaternion> LocalRotations;
            public NativeArray<Vector3> Positions;
            public NativeArray<int> Markers;
            public TransformStreamHandle ModelRoot;
            public TransformStreamHandle Spine;
            public TransformStreamHandle UpperChest;
            public TransformStreamHandle WeaponBone;
            public TransformStreamHandle IkWeaponBone;
            public int Stage;

            public void ProcessAnimation(AnimationStream stream)
            {
                int offset = Stage * LayerPoseProbeBoneCount;
                Rotations[offset] = ModelRoot.GetRotation(stream);
                Rotations[offset + 1] = Spine.GetRotation(stream);
                Rotations[offset + 2] = UpperChest.GetRotation(stream);
                Rotations[offset + 3] = WeaponBone.GetRotation(stream);
                Rotations[offset + 4] = IkWeaponBone.GetRotation(stream);
                Positions[offset] = ModelRoot.GetPosition(stream);
                Positions[offset + 1] = Spine.GetPosition(stream);
                Positions[offset + 2] = UpperChest.GetPosition(stream);
                Positions[offset + 3] = WeaponBone.GetPosition(stream);
                Positions[offset + 4] = IkWeaponBone.GetPosition(stream);
                LocalRotations[offset] = ModelRoot.GetLocalRotation(stream);
                LocalRotations[offset + 1] = Spine.GetLocalRotation(stream);
                LocalRotations[offset + 2] = UpperChest.GetLocalRotation(stream);
                LocalRotations[offset + 3] = WeaponBone.GetLocalRotation(stream);
                LocalRotations[offset + 4] = IkWeaponBone.GetLocalRotation(stream);
                Markers[Stage] = 1;
            }

            public void ProcessRootMotion(AnimationStream stream)
            {
            }
        }
#endif
    }
}
