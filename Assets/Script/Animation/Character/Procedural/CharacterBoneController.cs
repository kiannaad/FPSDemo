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
        private float diagnosticElapsed;
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
                output.SetSourcePlayable(blendingPlayable);

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

            DestroyPlayable(blendingPlayable);
            DestroyPlayable(virtualElementPlayable);

            DisposeNativeArrays();
            activeProfile = null;
            nextProfile = preservedProfile;
            shouldLinkProfile = preserveRequest;

            output = AnimationPlayableOutput.Null;
            virtualElementPlayable = AnimationScriptPlayable.Null;
            blendingPlayable = AnimationScriptPlayable.Null;
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
            blendingPlayable.ConnectInput(0, virtualElementPlayable, 0, 1f);
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

        private ProfileRuntime BuildRuntime(BoneProfile profile)
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
                        previousLayerPlayable = playable;
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
            ProfileRuntime requestedRuntime = requestedProfile != null
                ? BuildRuntime(requestedProfile)
                : null;
            ProfileRuntime previousRuntime = activeRuntime;
            BoneProfile previousProfile = activeProfile;

            previousRuntime?.DisconnectSource();
            if (blendingPlayable.GetInput(0).IsValid())
            {
                blendingPlayable.DisconnectInput(0);
            }
            requestedRuntime?.ConnectSource(virtualElementPlayable);
            ConnectBlendingSource(
                requestedRuntime?.ResolveFinalPlayable(virtualElementPlayable)
                ?? virtualElementPlayable);

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

            requestedRuntime?.ConnectSource(virtualElementPlayable);
            ConnectBlendingSource(
                requestedRuntime?.ResolveFinalPlayable(virtualElementPlayable)
                ?? virtualElementPlayable);
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

        private void UpdateRuntime(ProfileRuntime runtime, float deltaTime)
        {
            if (runtime == null)
            {
                return;
            }

            runtime.Update(Owner, deltaTime);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            string[] names = new string[profile.Layers.Count];
            for (int index = 0; index < profile.Layers.Count; index++)
            {
                names[index] = profile.Layers[index].GetType().Name;
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
public void Add(AnimationLayer layer)
            {
                layers.Add(layer ?? throw new ArgumentNullException(nameof(layer)));
            }

            public Playable ResolveFinalPlayable(Playable emptySource)
            {
                return layers.Count == 0 ? emptySource : layers[layers.Count - 1].Playable;
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

                // Turn produces the visual ModelRoot offset that Look consumes.
                // Prepare that cross-layer input first without changing the
                // Playable evaluation order (Look must still write before Turn).
                for (int index = 0; index < layers.Count; index++)
                {
                    if (layers[index].Settings is TurnLayerSettings)
                    {
                        layers[index].PreUpdate(deltaTime, weights[index]);
                    }
                }

                for (int index = 0; index < layers.Count; index++)
                {
                    if (!(layers[index].Settings is TurnLayerSettings))
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
                for (int index = layers.Count - 1; index >= 0; index--)
                {
                    layers[index].Dispose();
                }

                layers.Clear();
                isDisposed = true;
            }
        }
    }
}
