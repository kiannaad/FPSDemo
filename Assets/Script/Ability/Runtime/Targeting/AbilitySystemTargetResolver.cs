using UnityEngine;

namespace CGame.Ability.Targeting
{
    public sealed class AbilitySystemTargetResolver : IAbilitySystemTargetResolver
    {
        public bool TryResolve(Collider collider, out AbilitySystemComponent abilitySystem)
        {
            abilitySystem = null;
            if (collider == null || !collider.gameObject.activeInHierarchy)
            {
                return false;
            }

            Transform hitTransform = collider.transform;
            foreach (AbilitySystemComponent candidate in AbilitySystemComponent.ActiveComponents)
            {
                if (candidate == null || candidate.IsDisposed ||
                    !(candidate.Avatar is IAbilitySystemAvatar avatar))
                {
                    continue;
                }

                GameObject root = avatar.AbilitySystemRoot;
                if (root == null || !root.activeInHierarchy)
                {
                    continue;
                }

                Transform rootTransform = root.transform;
                if (hitTransform == rootTransform || hitTransform.IsChildOf(rootTransform))
                {
                    abilitySystem = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
