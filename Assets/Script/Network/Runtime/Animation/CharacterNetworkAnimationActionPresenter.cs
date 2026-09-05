using System;
using CGame.Animation;

namespace CGame.Network
{
    public interface INetworkAnimationActionAssetResolver
    {
        bool TryResolve(NetworkAnimationActionStarted action, out AnimationClipAsset asset);
    }

    public sealed class EmptyNetworkAnimationActionAssetResolver : INetworkAnimationActionAssetResolver
    {
        public static readonly EmptyNetworkAnimationActionAssetResolver Instance =
            new EmptyNetworkAnimationActionAssetResolver();

        private EmptyNetworkAnimationActionAssetResolver()
        {
        }

        public bool TryResolve(NetworkAnimationActionStarted action, out AnimationClipAsset asset)
        {
            asset = null;
            return false;
        }
    }

    public sealed class CharacterNetworkAnimationActionPresenter : INetworkAnimationActionPresenter
    {
        private readonly PawnAnimationComponent animation;
        private readonly INetworkAnimationActionAssetResolver resolver;

        public CharacterNetworkAnimationActionPresenter(
            PawnAnimationComponent animation,
            INetworkAnimationActionAssetResolver resolver)
        {
            this.animation = animation ?? throw new ArgumentNullException(nameof(animation));
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public bool TryPlay(
            NetworkAnimationActionStarted action,
            float elapsedSeconds,
            out object playbackHandle)
        {
            playbackHandle = null;
            if (animation.AnimInstance == null || !resolver.TryResolve(action, out AnimationClipAsset asset))
                return false;
            AnimationPlaybackHandle handle = animation.AnimInstance.PlayAbilityAnimationAtSeconds(
                asset,
                action.ActionSequence,
                elapsedSeconds);
            if (handle == null || handle.State == AnimationPlaybackState.Failed) return false;
            playbackHandle = handle;
            return true;
        }

        public void Stop(object playbackHandle)
        {
            if (playbackHandle is AnimationPlaybackHandle handle)
                animation.AnimInstance?.StopAbilityAnimation(handle);
        }
    }
}
