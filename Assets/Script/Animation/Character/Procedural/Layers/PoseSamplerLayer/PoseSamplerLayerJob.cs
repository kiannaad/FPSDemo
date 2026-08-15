using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class PoseSamplerLayerJob : IAnimationLayerJob
    {
        private Animator animator;
        private PoseSamplerLayerSettings settings;
        private PoseSamplerJob job;

        public Type SettingsType => typeof(PoseSamplerLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = RequireSettings(layerSettings);
            animator = jobData.Animator;
            Transform root = animator.transform;
            Transform pelvis = Resolve(jobData, settings.Pelvis, "pelvis");
            Transform spine = Resolve(jobData, settings.SpineRoot, "spine root");
            Transform weapon = Resolve(jobData, settings.WeaponBone, "weapon bone");
            Transform weaponRight = Resolve(jobData, settings.WeaponBoneRight, "right weapon bone");
            Transform weaponLeft = Resolve(jobData, settings.WeaponBoneLeft, "left weapon bone");

            jobData.RigComponent.RestoreInitializedHierarchyPose();

            KTransform defaultWeapon = settings.DefaultWeaponPose;
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
            Quaternion cachedPelvisPose = Quaternion.Inverse(root.rotation) * pelvis.rotation;
            KTransform componentPose = new KTransform(root).GetRelativeTransform(new KTransform(weapon), false);
            KTransform spinePose = new KTransform(spine).GetRelativeTransform(new KTransform(weapon), false);

            jobData.RigComponent.RestoreInitializedHierarchyPose();

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
                WeaponBoneRight = Bind(jobData, settings.WeaponBoneRight, "right weapon bone"),
                WeaponBoneLeft = Bind(jobData, settings.WeaponBoneLeft, "left weapon bone"),
                IkWeaponBone = Bind(jobData, settings.IkWeaponBone, "IK weapon bone"),
                IkRightHand = Bind(jobData, settings.IkRightHand, "IK right hand"),
                IkLeftHand = Bind(jobData, settings.IkLeftHand, "IK left hand"),
                IkRightHandHint = Bind(jobData, settings.IkRightHandHint, "IK right hint"),
                IkLeftHandHint = Bind(jobData, settings.IkLeftHandHint, "IK left hint"),
                CachedPelvisPose = cachedPelvisPose,
                WeaponBoneComponentPose = componentPose,
                WeaponBoneSpinePose = spinePose,
                WeaponBoneOffset = settings.WeaponBoneOffset,
                HasValidRoot = hasValidRoot
            };
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, job, 1);
        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate() { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            job.Weight = weight;
            job.OverwriteRoot = settings.OverwriteRoot;
            job.OverwriteWeaponBone = settings.OverwriteWeaponBone;
            job.DefaultWeaponPose = settings.DefaultWeaponPose;
            job.WeaponBoneOffset = settings.WeaponBoneOffset;
            job.StabilizationWeight = settings.StabilizationWeight;
            job.WeaponBoneWeight = string.IsNullOrWhiteSpace(settings.WeaponBoneWeightCurve)
                ? 0f
                : animator.GetFloat(settings.WeaponBoneWeightCurve);
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }

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
