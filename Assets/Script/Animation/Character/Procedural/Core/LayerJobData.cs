using System;
using CGame.Animation.Rig;
using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public readonly struct LayerJobData
    {
        public LayerJobData(
            Animator animator,
            KRigComponent rigComponent,
            TransformStreamHandle visualRootHandle,
            CharacterAnimInstance owner)
        {
            Animator = animator ?? throw new ArgumentNullException(nameof(animator));
            RigComponent = rigComponent ?? throw new ArgumentNullException(nameof(rigComponent));
            VisualRootHandle = visualRootHandle;
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public Animator Animator { get; }
        public KRigComponent RigComponent { get; }
        public TransformStreamHandle VisualRootHandle { get; }
        public CharacterAnimInstance Owner { get; }
        public AnimationUpdateContext UpdateContext => Owner.UpdateContext;
    }
}
