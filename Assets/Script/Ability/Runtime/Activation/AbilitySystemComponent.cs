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
        private readonly HashSet<AbilitySpecHandle> removeWhenEnded = new HashSet<AbilitySpecHandle>();
        private readonly HashSet<AbilitySpecHandle> pressedInputHandles = new HashSet<AbilitySpecHandle>();
        private readonly HashSet<AbilitySpecHandle> heldInputHandles = new HashSet<AbilitySpecHandle>();
        private readonly HashSet<AbilitySpecHandle> releasedInputHandles = new HashSet<AbilitySpecHandle>();
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
            pressedInputHandles.Clear();
            heldInputHandles.Clear();
            releasedInputHandles.Clear();
            Avatar = null;
            IsDisposed = true;
        }

        public AbilitySpecHandle GiveAbility(AbilityDefinition definition, object sourceObject)
        {
            return GiveAbility(new AbilityGrantDefinition(definition), sourceObject);
        }

        public AbilitySpecHandle GiveAbility(AbilityGrantDefinition grant, object sourceObject)
        {
            return GiveAbility(grant, sourceObject, null);
        }

        private AbilitySpecHandle GiveAbility(AbilityGrantDefinition grant, object sourceObject, ISet<AbilitySpecHandle> replacementHandles)
        {
            if (grant == null)
            {
                throw new ArgumentNullException(nameof(grant));
            }

            if (!grant.InputTag.IsEmpty && !IsExplicitLeafTag(grant.InputTag))
            {
                throw new ArgumentException("InputTag must be a registered explicit leaf GameplayTag.", nameof(grant));
            }

            if (!grant.InputTag.IsEmpty && specs.Values.Any(spec => spec.InputTag == grant.InputTag && (replacementHandles == null || !replacementHandles.Contains(spec.Handle))))
            {
                throw new InvalidOperationException("An AbilitySystemComponent can only grant one spec for a non-empty InputTag.");
            }

            var handle = new AbilitySpecHandle(++nextSpecHandle);
            var spec = new AbilitySpec(handle, grant.Definition, sourceObject, grant.InputTag);
            spec.PrimaryInstance = grant.Definition.CreateInstanceForSpec();
            specs.Add(handle, spec);
            return handle;
        }

        public bool TryGetSpec(AbilitySpecHandle handle, out AbilitySpec spec)
        {
            return specs.TryGetValue(handle, out spec);
        }

        public AbilityGrantReceipt GiveAbilitySet(AbilitySet abilitySet, object sourceObject, IReadOnlyList<AbilityGrantReceipt> replacedReceipts = null)
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
            var replacementHandles = new HashSet<AbilitySpecHandle>();
            if (replacedReceipts != null)
            {
                foreach (AbilityGrantReceipt replacedReceipt in replacedReceipts.Where(candidateReceipt => candidateReceipt != null && candidateReceipt.IsActive))
                {
                    foreach (AbilitySpecHandle handle in replacedReceipt.SpecHandles) replacementHandles.Add(handle);
                }
            }
            try
            {
                foreach (AbilityGrantDefinition grant in abilitySet.Grants)
                {
                    specHandles.Add(GiveAbility(grant, sourceObject, replacementHandles));
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
            RemoveInputState(spec);
            removeWhenEnded.Remove(handle);
            specs.Remove(handle);
            return true;
        }

        public void AbilityInputTagPressed(GameplayTag inputTag)
        {
            AbilitySpec spec = specs.Values.SingleOrDefault(candidate => candidate.InputTag == inputTag);
            if (spec == null)
            {
                return;
            }

            spec.InputPressed = true;
            pressedInputHandles.Add(spec.Handle);
            heldInputHandles.Add(spec.Handle);
        }

        public void AbilityInputTagReleased(GameplayTag inputTag)
        {
            AbilitySpec spec = specs.Values.SingleOrDefault(candidate => candidate.InputTag == inputTag);
            if (spec == null)
            {
                return;
            }

            spec.InputPressed = false;
            heldInputHandles.Remove(spec.Handle);
            releasedInputHandles.Add(spec.Handle);
        }

        public void ClearAbilityInput()
        {
            foreach (AbilitySpecHandle handle in heldInputHandles.ToArray())
            {
                if (specs.TryGetValue(handle, out AbilitySpec spec))
                {
                    spec.InputPressed = false;
                    spec.PrimaryInstance.NotifyInputReleased();
                }
            }

            pressedInputHandles.Clear();
            heldInputHandles.Clear();
            releasedInputHandles.Clear();
        }

        public void ProcessAbilityInput()
        {
            var activationRequests = new HashSet<AbilitySpecHandle>();
            foreach (AbilitySpecHandle handle in heldInputHandles.ToArray())
            {
                if (specs.TryGetValue(handle, out AbilitySpec spec) &&
                    spec.PrimaryInstance.State == AbilityInstanceState.Inactive &&
                    spec.Definition.InputActivationPolicy == AbilityInputActivationPolicy.WhileInputActive)
                {
                    activationRequests.Add(handle);
                }
            }

            foreach (AbilitySpecHandle handle in pressedInputHandles.ToArray())
            {
                if (!specs.TryGetValue(handle, out AbilitySpec spec)) continue;
                if (spec.PrimaryInstance.State == AbilityInstanceState.Active)
                {
                    spec.PrimaryInstance.NotifyInputPressed();
                }
                else if (spec.Definition.InputActivationPolicy == AbilityInputActivationPolicy.OnInputTriggered)
                {
                    activationRequests.Add(handle);
                }
            }

            foreach (AbilitySpecHandle handle in activationRequests)
            {
                TryActivateAbility(handle);
            }

            foreach (AbilitySpecHandle handle in releasedInputHandles.ToArray())
            {
                if (specs.TryGetValue(handle, out AbilitySpec spec)) spec.PrimaryInstance.NotifyInputReleased();
            }

            pressedInputHandles.Clear();
            releasedInputHandles.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (IsDisposed || deltaTime <= 0f)
            {
                return;
            }

            foreach (AbilitySpec spec in specs.Values.ToArray())
            {
                if (spec.PrimaryInstance.State == AbilityInstanceState.Active)
                {
                    spec.PrimaryInstance.TickTasks(deltaTime);
                }
            }
        }

        public AbilityActivationResult TryActivateAbility(AbilitySpecHandle handle)
        {
            if (!specs.TryGetValue(handle, out AbilitySpec spec))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.NotFound });
            }

            return TryActivateSpec(spec, null);
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

            return TryActivateSpec(matches[0], null);
        }

        public AbilityActivationResult TriggerAbilityFromGameplayEvent(
            AbilitySpecHandle handle,
            GameplayTag eventTag,
            AbilityGameEventPayload payload)
        {
            if (!specs.TryGetValue(handle, out AbilitySpec spec))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.NotFound });
            }

            if (!IsExplicitLeafTag(eventTag) || !spec.Definition.TriggerEventTags.Contains(eventTag))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.InvalidEventTag });
            }

            if (payload == null || !ReferenceEquals(payload.TargetAbilitySystem, this) || !ReferenceEquals(payload.Avatar, Avatar))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.InvalidEventPayload });
            }

            return TryActivateSpec(spec, payload);
        }

        public AbilityActivationResult GiveAbilityAndActivateOnce(AbilityDefinition definition, object sourceObject)
        {
            AbilitySpecHandle handle = GiveAbility(definition, sourceObject);
            removeWhenEnded.Add(handle);
            AbilityActivationResult result = TryActivateAbility(handle);
            if (!result.Succeeded)
            {
                RemoveAbility(handle);
            }

            return result;
        }

        internal void NotifyAbilityEnded(AbilitySpecHandle handle)
        {
            if (removeWhenEnded.Contains(handle))
            {
                RemoveAbility(handle, AbilityEndReason.Completed);
            }
        }

        private AbilityActivationResult TryActivateSpec(AbilitySpec spec, AbilityGameEventPayload eventPayload)
        {
            if (Avatar == null)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.InvalidAvatar });
            }

            if (spec.SourceObject == null)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.InvalidSource });
            }

            if (executionGate != null && !executionGate.CanExecuteLocally(this))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.NotLocal });
            }

            if (spec.Definition.RequiredOwnedTags.Any(tag => !HasOwnedTag(tag)))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.RequiredTagMissing });
            }

            if (spec.Definition.BlockedOwnedTags.Any(HasOwnedTag))
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.BlockedTagPresent });
            }

            if (spec.PrimaryInstance.State != AbilityInstanceState.Inactive)
            {
                return AbilityActivationResult.Failure(new[] { AbilityFailureTags.AlreadyActive });
            }

            var activationHandle = new AbilityActivationHandle(nextActivationHandle + 1);
            var context = new AbilityActivationContext(this, spec, Avatar, activationHandle, eventPayload);
            if (!spec.PrimaryInstance.CanActivate(context))
            {
                return AbilityActivationResult.Failure();
            }

            foreach (AbilitySpec targetSpec in specs.Values
                         .Where(candidate =>
                             candidate.PrimaryInstance.State == AbilityInstanceState.Active &&
                             spec.Definition.CancelAbilityTags.Contains(candidate.Definition.AbilityTag))
                         .ToArray())
            {
                CancelAbility(targetSpec.Handle, AbilityEndReason.Cancelled);
            }

            nextActivationHandle++;
            spec.PrimaryInstance.Activate(context);
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

        private static bool IsExplicitLeafTag(GameplayTag tag)
        {
            return !tag.IsEmpty &&
                   GameplayTagManager.Instance.IsExplicitTag(tag) &&
                   GameplayTagManager.Instance.GetDirectChildren(tag).Count == 0;
        }

        private void RemoveInputState(AbilitySpec spec)
        {
            spec.InputPressed = false;
            pressedInputHandles.Remove(spec.Handle);
            heldInputHandles.Remove(spec.Handle);
            releasedInputHandles.Remove(spec.Handle);
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
