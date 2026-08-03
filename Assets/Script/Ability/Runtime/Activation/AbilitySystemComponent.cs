using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public sealed class AbilitySystemComponent
    {
        private readonly Dictionary<AbilitySpecHandle, AbilitySpec> specs = new Dictionary<AbilitySpecHandle, AbilitySpec>();
        private readonly Dictionary<GameplayTag, int> ownedTagCounts = new Dictionary<GameplayTag, int>();
        private readonly Dictionary<int, GameplayTag> ownedTagGrants = new Dictionary<int, GameplayTag>();
        private readonly List<AbilityGrantReceipt> grantReceipts = new List<AbilityGrantReceipt>();
        private readonly List<GameEventListener> gameEventListeners = new List<GameEventListener>();
        private readonly IAbilityExecutionGate executionGate;
        private int nextSpecHandle;
        private int nextTagGrantHandle;
        private long nextActivationHandle;
        private long nextGameEventRegistrationId;

        public AbilitySystemComponent(object avatar, IAbilityExecutionGate executionGate = null)
        {
            Avatar = avatar;
            this.executionGate = executionGate;
        }

        public object Avatar { get; private set; }
        public int AbilityCount => specs.Count;
        public bool IsDisposed { get; private set; }

        public bool SetAvatar(object avatar)
        {
            if (ReferenceEquals(Avatar, avatar))
            {
                return false;
            }

            EndAllActiveAbilities(AbilityEndReason.AvatarChanged);
            Avatar = avatar;
            return true;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            foreach (AbilityGrantReceipt receipt in grantReceipts.ToArray())
            {
                receipt.Revoke();
            }

            EndAllActiveAbilities(AbilityEndReason.SourceRemoved);
            specs.Clear();
            ownedTagGrants.Clear();
            ownedTagCounts.Clear();
            gameEventListeners.Clear();
            Avatar = null;
            IsDisposed = true;
        }

        public AbilitySpecHandle GiveAbility(AbilityDefinition definition, object sourceObject)
        {
            var handle = new AbilitySpecHandle(++nextSpecHandle);
            var spec = new AbilitySpec(handle, definition, sourceObject);
            spec.PrimaryInstance = definition.CreateInstanceForSpec();
            specs.Add(handle, spec);
            return handle;
        }

        public bool TryGetSpec(AbilitySpecHandle handle, out AbilitySpec spec)
        {
            return specs.TryGetValue(handle, out spec);
        }

        public AbilityGrantReceipt GiveAbilitySet(AbilitySet abilitySet, object sourceObject)
        {
            if (abilitySet == null)
            {
                throw new System.ArgumentNullException(nameof(abilitySet));
            }

            if (sourceObject == null)
            {
                throw new System.ArgumentNullException(nameof(sourceObject));
            }

            AbilityGrantReceipt existing = grantReceipts.FirstOrDefault(receipt =>
                receipt.IsActive &&
                ReferenceEquals(receipt.AbilitySet, abilitySet) &&
                ReferenceEquals(receipt.SourceObject, sourceObject));
            if (existing != null)
            {
                return existing;
            }

            var specHandles = new List<AbilitySpecHandle>();
            var tagHandles = new List<GameplayTagGrantHandle>();
            try
            {
                foreach (AbilityDefinition definition in abilitySet.Abilities)
                {
                    specHandles.Add(GiveAbility(definition, sourceObject));
                }

                foreach (GameplayTag tag in abilitySet.OwnedTags)
                {
                    tagHandles.Add(AddOwnedTag(tag));
                }
            }
            catch
            {
                for (int index = specHandles.Count - 1; index >= 0; index--)
                {
                    RemoveAbility(
                        specHandles[index],
                        AbilityEndReason.SourceRemoved);
                }

                for (int index = tagHandles.Count - 1; index >= 0; index--)
                {
                    RemoveOwnedTag(tagHandles[index]);
                }

                throw;
            }

            var receipt = new AbilityGrantReceipt(this, abilitySet, sourceObject, specHandles, tagHandles);
            grantReceipts.Add(receipt);
            return receipt;
        }

        internal bool RevokeAbilitySet(AbilityGrantReceipt receipt)
        {
            if (receipt == null || !receipt.IsActive || !grantReceipts.Remove(receipt))
            {
                return false;
            }

            foreach (AbilitySpecHandle handle in receipt.SpecHandles)
            {
                RemoveAbility(handle, AbilityEndReason.SourceRemoved);
            }

            foreach (GameplayTagGrantHandle handle in receipt.TagGrantHandles)
            {
                RemoveOwnedTag(handle);
            }

            receipt.MarkRevoked();
            return true;
        }

        public bool RemoveAbility(AbilitySpecHandle handle, AbilityEndReason reason = AbilityEndReason.SourceRemoved)
        {
            if (!specs.TryGetValue(handle, out AbilitySpec spec))
            {
                return false;
            }

            spec.PrimaryInstance.EndAbility(reason);
            specs.Remove(handle);
            return true;
        }

        public AbilityActivationResult TryActivateAbilityByTag(GameplayTag abilityTag)
        {
            if (abilityTag.IsEmpty ||
                !GameplayTagManager.Instance.IsExplicitTag(abilityTag) ||
                GameplayTagManager.Instance.GetDirectChildren(abilityTag).Count > 0)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.InvalidAbilityTag });
            }

            AbilitySpec[] matches = specs.Values
                .Where(spec => spec.Definition.AbilityTag == abilityTag)
                .ToArray();
            if (matches.Length == 0)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.NotFound });
            }

            if (matches.Length > 1)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.Ambiguous });
            }

            if (Avatar == null)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.InvalidAvatar });
            }

            if (matches[0].SourceObject == null)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.InvalidSource });
            }

            if (executionGate != null && !executionGate.CanExecuteLocally(this))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.NotLocal });
            }

            if (matches[0].Definition.RequiredOwnedTags.Any(tag => !HasOwnedTag(tag)))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.RequiredTagMissing });
            }

            if (matches[0].Definition.BlockedOwnedTags.Any(HasOwnedTag))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.BlockedTagPresent });
            }

            if (matches[0].PrimaryInstance.State != AbilityInstanceState.Inactive)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.AlreadyActive });
            }

            var activationHandle = new AbilityActivationHandle(++nextActivationHandle);
            var context = new AbilityActivationContext(this, matches[0], Avatar, activationHandle);
            matches[0].PrimaryInstance.Activate(context);
            return AbilityActivationResult.Success(activationHandle);
        }

        public AbilityGameEventRegistration RegisterGameEvent(
            GameplayTag eventTag,
            AbilityGameEventMatchPolicy matchPolicy,
            Action<AbilityGameEventPayload> callback)
        {
            if (eventTag.IsEmpty || !GameplayTagManager.Instance.IsRegistered(eventTag))
            {
                throw new ArgumentException("A registered GameplayTag is required.", nameof(eventTag));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var listener = new GameEventListener(
                ++nextGameEventRegistrationId,
                eventTag,
                matchPolicy,
                callback);
            gameEventListeners.Add(listener);
            return new AbilityGameEventRegistration(this, listener.Id);
        }

        public AbilityGameEventDispatchResult HandleGameEvent(
            GameplayTag eventTag,
            AbilityGameEventPayload payload)
        {
            if (payload == null ||
                !ReferenceEquals(payload.TargetAbilitySystem, this) ||
                !ReferenceEquals(payload.Avatar, Avatar) ||
                eventTag.IsEmpty ||
                !GameplayTagManager.Instance.IsRegistered(eventTag))
            {
                return AbilityGameEventDispatchResult.Empty;
            }

            int matchedCount = 0;
            int invokedCount = 0;
            var exceptions = new List<Exception>();
            foreach (GameEventListener listener in gameEventListeners.ToArray())
            {
                if (!listener.IsActive || !MatchesGameEvent(listener, eventTag))
                {
                    continue;
                }

                matchedCount++;
                invokedCount++;
                try
                {
                    listener.Callback(payload);
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            return new AbilityGameEventDispatchResult(matchedCount, invokedCount, exceptions);
        }

        internal bool IsGameEventRegistrationActive(long registrationId)
        {
            return gameEventListeners.Any(listener => listener.Id == registrationId && listener.IsActive);
        }

        internal bool UnregisterGameEvent(long registrationId)
        {
            GameEventListener listener = gameEventListeners.FirstOrDefault(candidate => candidate.Id == registrationId);
            if (listener == null || !listener.IsActive)
            {
                return false;
            }

            listener.IsActive = false;
            return true;
        }

        public bool CancelAbility(AbilitySpecHandle handle, AbilityEndReason reason)
        {
            return specs.TryGetValue(handle, out AbilitySpec spec) && spec.PrimaryInstance.EndAbility(reason);
        }

        public GameplayTagGrantHandle AddOwnedTag(GameplayTag tag)
        {
            int handleValue = ++nextTagGrantHandle;
            ownedTagGrants.Add(handleValue, tag);
            ownedTagCounts.TryGetValue(tag, out int count);
            ownedTagCounts[tag] = count + 1;
            return new GameplayTagGrantHandle(handleValue, tag);
        }

        public bool RemoveOwnedTag(GameplayTagGrantHandle handle)
        {
            if (!handle.IsValid || !ownedTagGrants.TryGetValue(handle.Value, out GameplayTag tag))
            {
                return false;
            }

            ownedTagGrants.Remove(handle.Value);
            int remaining = ownedTagCounts[tag] - 1;
            if (remaining == 0)
            {
                ownedTagCounts.Remove(tag);
            }
            else
            {
                ownedTagCounts[tag] = remaining;
            }

            return true;
        }

        public bool HasOwnedTagExact(GameplayTag tag)
        {
            return ownedTagCounts.ContainsKey(tag);
        }

        public int GetOwnedTagCount(GameplayTag tag)
        {
            return ownedTagCounts.TryGetValue(tag, out int count) ? count : 0;
        }

        public bool HasOwnedTag(GameplayTag queryTag)
        {
            return ownedTagCounts.Keys.Any(ownedTag => GameplayTagManager.Instance.MatchesTag(ownedTag, queryTag));
        }

        private void EndAllActiveAbilities(AbilityEndReason reason)
        {
            foreach (AbilitySpec spec in specs.Values)
            {
                spec.PrimaryInstance.EndAbility(reason);
            }
        }

        private static bool MatchesGameEvent(GameEventListener listener, GameplayTag eventTag)
        {
            return listener.MatchPolicy == AbilityGameEventMatchPolicy.Exact
                ? listener.EventTag == eventTag
                : GameplayTagManager.Instance.MatchesTag(eventTag, listener.EventTag);
        }

        private sealed class GameEventListener
        {
            public GameEventListener(
                long id,
                GameplayTag eventTag,
                AbilityGameEventMatchPolicy matchPolicy,
                Action<AbilityGameEventPayload> callback)
            {
                Id = id;
                EventTag = eventTag;
                MatchPolicy = matchPolicy;
                Callback = callback;
                IsActive = true;
            }

            public long Id { get; }
            public GameplayTag EventTag { get; }
            public AbilityGameEventMatchPolicy MatchPolicy { get; }
            public Action<AbilityGameEventPayload> Callback { get; }
            public bool IsActive { get; set; }
        }
    }
}
