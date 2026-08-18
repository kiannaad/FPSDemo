using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class PoseSamplerLayerJob : IAnimationLayerJob
    {
        private Animator animator;
        private CharacterAnimInstance owner;
        private PoseSamplerLayerSettings settings;
        private PoseSamplerJob job;

        public Type SettingsType => typeof(PoseSamplerLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = RequireSettings(layerSettings);
            animator = jobData.Animator;
            owner = jobData.Owner;
            Transform root = animator.transform;
            Transform pelvis = Resolve(jobData, settings.Pelvis, "pelvis");
            Transform spine = Resolve(jobData, settings.SpineRoot, "spine root");
            Transform weapon = Resolve(jobData, settings.WeaponBone, "weapon bone");
            Transform weaponRight = Resolve(jobData, settings.WeaponBoneRight, "right weapon bone");
            Transform weaponLeft = Resolve(jobData, settings.WeaponBoneLeft, "left weapon bone");
            Transform camera = settings.UseReferenceHandTargets
                ? Resolve(jobData, settings.CameraBone, "camera bone")
                : null;

            KTransform defaultWeapon = settings.DefaultWeaponPose;
            KTransform[] cachedHierarchyPose = CaptureHierarchyPose(jobData);
            jobData.RigComponent.RestoreInitializedHierarchyPose();
            Quaternion cachedPelvisPose;
            KTransform componentPose;
            KTransform spinePose;
            KTransform rightReferencePose;
            KTransform leftReferencePose;
            KTransform rightHandReferencePose;
            KTransform leftHandReferencePose;
            KTransform rightHintReferencePose;
            KTransform leftHintReferencePose;
            KTransform weaponBoneCameraPose;
            try
            {
                weapon.position = root.TransformPoint(defaultWeapon.Position);
                weapon.rotation = root.rotation * defaultWeapon.Rotation;
                if (settings.ReferencePose != null)
                {
                    settings.ReferencePose.SampleAnimation(root.gameObject, 0f);
                }
                if (settings.OverwriteWeaponBone)
                {
                    weapon.position = root.TransformPoint(defaultWeapon.Position);
                    weapon.rotation = root.rotation * defaultWeapon.Rotation;
                }

                weaponRight.SetPositionAndRotation(weapon.position, weapon.rotation);
                weaponLeft.SetPositionAndRotation(weapon.position, weapon.rotation);
                cachedPelvisPose = Quaternion.Inverse(root.rotation) * pelvis.rotation;
                componentPose = new KTransform(root).GetRelativeTransform(new KTransform(weapon), false);
                spinePose = new KTransform(spine).GetRelativeTransform(new KTransform(weapon), false);
                rightReferencePose = new KTransform(weaponRight, false);
                leftReferencePose = new KTransform(weaponLeft, false);
                weaponBoneCameraPose = settings.UseReferenceHandTargets
                    ? new KTransform(camera).GetRelativeTransform(new KTransform(weapon), false)
                    : default;
                rightHandReferencePose = CaptureVirtualTargetRelativeToWeapon(jobData, settings.IkRightHand, weapon, "right hand");
                leftHandReferencePose = CaptureVirtualTargetRelativeToWeapon(jobData, settings.IkLeftHand, weapon, "left hand");
                rightHintReferencePose = CaptureVirtualTargetRelativeToWeapon(jobData, settings.IkRightHandHint, weapon, "right hand hint");
                leftHintReferencePose = CaptureVirtualTargetRelativeToWeapon(jobData, settings.IkLeftHandHint, weapon, "left hand hint");
            }
            finally
            {
                RestoreHierarchyPose(jobData, cachedHierarchyPose);
            }

            bool hasValidRoot = pelvis.parent != null && pelvis.parent != root;
            job = new PoseSamplerJob
            {
                CharacterRoot = jobData.CharacterRootHandle,
                SpineRoot = Bind(jobData, settings.SpineRoot, "spine root"),
                Pelvis = Bind(jobData, settings.Pelvis, "pelvis"),
                PelvisParent = settings.OverwriteRoot && hasValidRoot
                    ? animator.BindStreamTransform(pelvis.parent)
                    : default,
                WeaponBone = Bind(jobData, settings.WeaponBone, "weapon bone"),
                Camera = settings.UseReferenceHandTargets
                    ? Bind(jobData, settings.CameraBone, "camera bone")
                    : default,
                WeaponBoneRight = Bind(jobData, settings.WeaponBoneRight, "right weapon bone"),
                WeaponBoneLeft = Bind(jobData, settings.WeaponBoneLeft, "left weapon bone"),
                IkWeaponBone = Bind(jobData, settings.IkWeaponBone, "IK weapon bone"),
                IkRightHand = Bind(jobData, settings.IkRightHand, "IK right hand"),
                IkLeftHand = Bind(jobData, settings.IkLeftHand, "IK left hand"),
                IkRightHandHint = Bind(jobData, settings.IkRightHandHint, "IK right hint"),
                IkLeftHandHint = Bind(jobData, settings.IkLeftHandHint, "IK left hint"),
                CachedPelvisPose = cachedPelvisPose,
                WeaponBoneComponentPose = componentPose,
                WeaponBoneCameraPose = weaponBoneCameraPose,
                WeaponBoneSpinePose = spinePose,
                WeaponBoneRightLocalPose = rightReferencePose,
                WeaponBoneLeftLocalPose = leftReferencePose,
                WeaponBoneOffset = settings.WeaponBoneOffset,
                RightHandReferencePose = rightHandReferencePose,
                LeftHandReferencePose = leftHandReferencePose,
                RightHintReferencePose = rightHintReferencePose,
                LeftHintReferencePose = leftHintReferencePose,
                HasValidRoot = hasValidRoot
            };
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, job, 1);
        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight) { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            job.Weight = weight;
            job.OverwriteRoot = settings.OverwriteRoot;
            job.OverwriteWeaponBone = settings.OverwriteWeaponBone;
            job.DefaultWeaponPose = settings.DefaultWeaponPose;
            job.WeaponBoneOffset = settings.WeaponBoneOffset;
            job.StabilizationWeight = settings.StabilizationWeight;
            job.WeaponBoneWeight = string.IsNullOrWhiteSpace(settings.WeaponBoneWeightCurve)
                ? settings.DefaultWeaponBoneWeight
                : owner.GetCurveValue(settings.WeaponBoneWeightCurve);
            job.UseReferenceHandTargets = settings.UseReferenceHandTargets;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }

        private static KTransform CaptureVirtualTargetRelativeToWeapon(
            LayerJobData data,
            CGame.Animation.Rig.KRigElement element,
            Transform weapon,
            string label)
        {
            Transform virtualTransform = Resolve(data, element, label);
            CGame.Animation.Rig.KVirtualElement virtualElement = virtualTransform.GetComponent<CGame.Animation.Rig.KVirtualElement>();
            if (virtualElement == null || virtualElement.TargetBone == null)
            {
                throw new InvalidOperationException(label + " virtual target must reference a source bone.");
            }

            return new KTransform(weapon).GetRelativeTransform(new KTransform(virtualElement.TargetBone), false);
        }

        private static KTransform[] CaptureHierarchyPose(LayerJobData data)
        {
            int count = data.RigComponent.Rig.Hierarchy.Count;
            var pose = new KTransform[count];
            for (int index = 0; index < count; index++)
            {
                pose[index] = new KTransform(data.RigComponent.GetRigTransform(index), false);
            }

            return pose;
        }

        private static void RestoreHierarchyPose(LayerJobData data, KTransform[] pose)
        {
            for (int index = 0; index < pose.Length; index++)
            {
                Transform target = data.RigComponent.GetRigTransform(index);
                target.localPosition = pose[index].Position;
                target.localRotation = pose[index].Rotation;
                target.localScale = pose[index].Scale;
            }
        }

        private static PoseSamplerLayerSettings RequireSettings(AnimationLayerSettings value)
        {
            return value as PoseSamplerLayerSettings
                ?? throw new ArgumentException("Pose sampler job requires PoseSamplerLayerSettings.", nameof(value));
        }

        private static Transform Resolve(LayerJobData data, CGame.Animation.Rig.KRigElement element, string label)
        {
            return RigHandleUtility.ResolveTransform(data.RigComponent, element, label);
        }

        private static TransformStreamHandle Bind(LayerJobData data, CGame.Animation.Rig.KRigElement element, string label)
        {
            return RigHandleUtility.Bind(data.Animator, data.RigComponent, element, label);
        }
    }
}
