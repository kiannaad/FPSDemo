using System;
using CGame.Animation;

namespace CGame.Ability.Animation
{
    public sealed class PlayAnimationAbilityTask : AbilityTask
    {
        private readonly IAbilityAnimationPlayer player;
        private readonly AnimationClipAsset asset;
        private readonly long requestId;
        private readonly Action<AbilityAnimationPlaybackState> callback;

        public PlayAnimationAbilityTask(
            IAbilityAnimationPlayer player,
            AnimationClipAsset asset,
            long requestId,
            Action<AbilityAnimationPlaybackState> callback)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.asset = asset ?? throw new ArgumentNullException(nameof(asset));
            this.requestId = requestId;
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public IAbilityAnimationPlayback Playback { get; private set; }

        protected override void OnActivate()
        {
            player.Updated += HandlePlayerUpdated;
            Playback = player.PlayAnimation(asset, requestId);
            EvaluatePlayback();
        }

        protected override void OnCompleted()
        {
            player.Updated -= HandlePlayerUpdated;
        }

        protected override void OnCancelled()
        {
            player.Updated -= HandlePlayerUpdated;
            if (Playback != null && !Playback.IsTerminal)
            {
                player.StopAnimation(Playback);
            }
        }

        private void HandlePlayerUpdated()
        {
            EvaluatePlayback();
        }

        private void EvaluatePlayback()
        {
            if (State != AbilityTaskState.Active || Playback == null || !Playback.IsTerminal)
            {
                return;
            }

            AbilityAnimationPlaybackState terminalState = Playback.State;
            CompleteTask();
            callback(terminalState);
        }
    }
}
