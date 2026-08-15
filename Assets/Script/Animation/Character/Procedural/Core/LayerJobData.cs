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
            TransformStreamHandle characterRootHandle,
            AnimationUpdateContext updateContext)
        {
            Animator = animator ?? throw new ArgumentNullException(nameof(animator));
            RigComponent = rigComponent ?? throw new ArgumentNullException(nameof(rigComponent));
            CharacterRootHandle = characterRootHandle;
            UpdateContext = updateContext ?? throw new ArgumentNullException(nameof(updateContext));
        }

        public Animator Animator { get; }
        public KRigComponent RigComponent { get; }
        public TransformStreamHandle CharacterRootHandle { get; }
        public AnimationUpdateContext UpdateContext { get; }
    }
}
