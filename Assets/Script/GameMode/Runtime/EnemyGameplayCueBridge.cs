using System;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using CGame.GameplayTags;
using CGame.Network;
using UnityEngine;

namespace CGame
{
    public sealed class EnemyGameplayCueBridge : IEnemyActionCueSink
    {
        public bool TryExecute(EnemyActionEvent action, GameObject presentationRoot)
        {
            if (action == null || presentationRoot == null) return false;
            string tagName = action.ActionKind == EnemyActionKind.Fire ? "GameplayCue.Enemy.Fire" :
                action.ActionKind == EnemyActionKind.Hit ? "GameplayCue.Enemy.Impact" : null;
            if (tagName == null) return false;
            if (GameplayCueRouter.Current == null || !GameplayTagManager.Instance.TryRequestTag(tagName, out GameplayTag tag))
            {
                Debug.LogWarning($"[EnemyCue][060] Skipped EnemyId={action.EnemyId} Sequence={action.ActionSequence} Action={action.ActionKind} Reason=RouteUnavailable");
                return false;
            }
            try
            {
                var context = new GameplayEffectContext(presentationRoot, presentationRoot, null);
                GameplayCueRouter.Current.Execute(tag, new GameplayCueParameters(context, presentationRoot, presentationRoot.transform.position, true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[EnemyCue][060] Failed EnemyId={action.EnemyId} Sequence={action.ActionSequence} Action={action.ActionKind} Error={exception.Message}");
                return false;
            }
        }
    }
}
