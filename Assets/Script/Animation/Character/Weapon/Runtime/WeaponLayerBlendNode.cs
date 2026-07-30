using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class WeaponLayerBlendNode : AnimationNodeBase
    {
        private readonly IAnimationPlayableNode baseNode;
        private readonly AvatarMask weaponMask;
        private AnimationGraphContext context;
        private AnimationLayerMixerPlayable mixerPlayable;
        private IWeaponAnimationLayer currentLayer;
        private IWeaponAnimationLayer nextLayer;
        private int currentSlot;
        private int nextSlot;
        private float blendDuration;
        private float blendElapsed;
        private bool isBlending;

        public WeaponLayerBlendNode(IAnimationPlayableNode baseNode, AvatarMask weaponMask)
        {
            this.baseNode = baseNode ?? throw new ArgumentNullException(nameof(baseNode));
            this.weaponMask = weaponMask != null
                ? weaponMask
                : throw new ArgumentNullException(nameof(weaponMask));
        }

        public IWeaponAnimationLayer CurrentLayer => currentLayer;
        public IWeaponAnimationLayer NextLayer => nextLayer;
        public bool IsBlending => isBlending;
        public float CurrentWeight { get; private set; }
        public float NextWeight { get; private set; }

        public void DetachBaseInput()
        {
            if (context != null
                && mixerPlayable.IsValid()
                && mixerPlayable.GetInput(0).IsValid())
            {
                context.Graph.Disconnect(mixerPlayable, 0);
            }
        }

        public void SetTarget(IWeaponAnimationLayer target, float duration)
        {
            if (target != null && target.IsDisposed)
            {
                throw new ArgumentException("A disposed weapon animation layer cannot become the target.", nameof(target));
            }

            RemoveNextLayer();
            nextLayer = target;
            blendDuration = Mathf.Max(0f, duration);
            blendElapsed = 0f;
            isBlending = currentLayer != null || nextLayer != null;

            if (nextLayer != null && IsInitialized)
            {
                nextSlot = currentSlot == 1 ? 2 : 1;
                ConnectLayer(nextLayer, nextSlot);
            }

            if (blendDuration <= 0f && IsInitialized)
            {
                CompleteBlend();
            }
        }

        public override void Update(AnimationGraphContext graphContext, float deltaTime)
        {
            baseNode.Update(graphContext, deltaTime);
            currentLayer?.Update(graphContext, deltaTime);
            nextLayer?.Update(graphContext, deltaTime);
            if (!isBlending)
            {
                return;
            }

            blendElapsed += Mathf.Max(0f, deltaTime);
            if (blendDuration <= 0f || blendElapsed >= blendDuration)
            {
                CompleteBlend();
            }
        }

        public override AnimationPoseHandle Evaluate(AnimationGraphContext graphContext)
        {
            baseNode.Evaluate(graphContext);
            currentLayer?.Evaluate(graphContext);
            nextLayer?.Evaluate(graphContext);
            ApplyWeights();
            return new AnimationPoseHandle(mixerPlayable, 1f, graphContext.EvaluateFrameId, nameof(WeaponLayerBlendNode));
        }

        public override AnimationNodeDebugSnapshot GetDebugSnapshot()
        {
            float visibleWeight = Mathf.Max(CurrentWeight, NextWeight);
            return new AnimationNodeDebugSnapshot(nameof(WeaponLayerBlendNode), mixerPlayable.IsValid(), visibleWeight, 3);
        }

        protected override void OnInitialize(AnimationGraphContext graphContext)
        {
            context = graphContext;
            mixerPlayable = AnimationLayerMixerPlayable.Create(graphContext.Graph, 3);
            baseNode.Initialize(graphContext);
            AnimationPoseHandle basePose = baseNode.Evaluate(graphContext);
            graphContext.Graph.Connect(basePose.Playable, 0, mixerPlayable, 0);
            mixerPlayable.SetInputWeight(0, 1f);
            if (nextLayer != null)
            {
                nextSlot = 1;
                ConnectLayer(nextLayer, nextSlot);
                if (blendDuration <= 0f)
                {
                    CompleteBlend();
                }
            }
        }

        protected override void OnDestroy()
        {
            baseNode.Destroy();
            DisposeLayer(currentLayer, currentSlot);
            DisposeLayer(nextLayer, nextSlot);
            currentLayer = null;
            nextLayer = null;
            currentSlot = 0;
            nextSlot = 0;
            context = null;
            isBlending = false;
            CurrentWeight = 0f;
            NextWeight = 0f;
        }

        private void ApplyWeights()
        {
            float progress = isBlending && blendDuration > 0f
                ? Mathf.Clamp01(blendElapsed / blendDuration)
                : isBlending ? 1f : 0f;
            float currentPoseWeight = currentLayer != null && currentLayer.IsPoseAvailable ? 1f : 0f;
            float nextPoseWeight = nextLayer != null && nextLayer.IsPoseAvailable ? 1f : 0f;
            CurrentWeight = currentPoseWeight * (isBlending ? 1f - progress : 1f);
            NextWeight = nextPoseWeight * (isBlending ? progress : 0f);
            mixerPlayable.SetInputWeight(0, 1f);
            if (currentSlot > 0)
            {
                mixerPlayable.SetInputWeight(currentSlot, CurrentWeight);
            }

            if (nextSlot > 0)
            {
                mixerPlayable.SetInputWeight(nextSlot, NextWeight);
            }
        }

        private void CompleteBlend()
        {
            IWeaponAnimationLayer oldCurrent = currentLayer;
            int oldCurrentSlot = currentSlot;
            currentLayer = nextLayer;
            currentSlot = nextLayer != null ? nextSlot : 0;
            nextLayer = null;
            nextSlot = 0;
            isBlending = false;
            blendElapsed = blendDuration;
            DisposeLayer(oldCurrent, oldCurrentSlot);
            CurrentWeight = currentLayer != null && currentLayer.IsPoseAvailable ? 1f : 0f;
            NextWeight = 0f;
            if (mixerPlayable.IsValid())
            {
                mixerPlayable.SetInputWeight(0, 1f);
                if (currentSlot > 0)
                {
                    mixerPlayable.SetInputWeight(currentSlot, CurrentWeight);
                }
            }
        }

        private void RemoveNextLayer()
        {
            DisposeLayer(nextLayer, nextSlot);
            nextLayer = null;
            nextSlot = 0;
            NextWeight = 0f;
        }

        private void ConnectLayer(IWeaponAnimationLayer layer, int slot)
        {
            layer.Initialize(context);
            AnimationPoseHandle pose = layer.Evaluate(context);
            if (!pose.Playable.IsValid())
            {
                layer.Destroy();
                throw new InvalidOperationException("Weapon animation layer produced an invalid playable.");
            }

            context.Graph.Connect(pose.Playable, 0, mixerPlayable, slot);
            mixerPlayable.SetLayerMaskFromAvatarMask((uint)slot, weaponMask);
            mixerPlayable.SetInputWeight(slot, 0f);
        }

        private void DisposeLayer(IWeaponAnimationLayer layer, int slot)
        {
            if (layer == null)
            {
                return;
            }

            Playable layerPlayable = Playable.Null;
            if (context != null && mixerPlayable.IsValid() && slot > 0)
            {
                layerPlayable = mixerPlayable.GetInput(slot);
                if (layerPlayable.IsValid())
                {
                    context.Graph.Disconnect(mixerPlayable, slot);
                }
            }

            layer.Destroy();
            if (context != null && context.Graph.IsValid() && layerPlayable.IsValid())
            {
                context.Graph.DestroySubgraph(layerPlayable);
            }
        }
    }
}
