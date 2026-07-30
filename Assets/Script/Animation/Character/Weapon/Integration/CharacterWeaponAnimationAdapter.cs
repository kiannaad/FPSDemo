using System;
using System.Collections.Generic;

namespace CGame.Animation
{
    public sealed class CharacterWeaponAnimationAdapter : IDisposable
    {
        private readonly Queue<WeaponAnimationEvent> pendingEvents =
            new Queue<WeaponAnimationEvent>();
        private WeaponRuntime boundRuntime;
        private bool isDisposed;

        public WeaponRuntime BoundRuntime => boundRuntime;
        public int PendingActionCount => pendingEvents.Count;
        public int PendingEventCount => pendingEvents.Count;

        public void BindRuntime(WeaponRuntime runtime)
        {
            if (isDisposed || boundRuntime == runtime)
            {
                return;
            }

            if (boundRuntime != null)
            {
                boundRuntime.ActionChanged -= OnActionChanged;
                boundRuntime.SwitchChanged -= OnSwitchChanged;
            }

            pendingEvents.Clear();
            boundRuntime = runtime;
            if (boundRuntime == null)
            {
                return;
            }

            boundRuntime.ActionChanged += OnActionChanged;
            boundRuntime.SwitchChanged += OnSwitchChanged;
            WeaponSwitchFact activeSwitch = boundRuntime.ActiveSwitch;
            if (activeSwitch.IsValid
                && activeSwitch.Phase == WeaponSwitchPhase.Started)
            {
                pendingEvents.Enqueue(
                    new WeaponAnimationEvent(activeSwitch));
                return;
            }

            WeaponActionFact activeAction = boundRuntime.ActiveAction;
            if (activeAction.IsValid
                && activeAction.Phase == WeaponActionPhase.Started)
            {
                pendingEvents.Enqueue(
                    new WeaponAnimationEvent(activeAction));
            }
        }

        public bool TryDequeue(out WeaponActionFact fact)
        {
            if (pendingEvents.Count == 0
                || pendingEvents.Peek().Kind
                    != WeaponAnimationEventKind.Action)
            {
                fact = default;
                return false;
            }

            fact = pendingEvents.Dequeue().Action;
            return true;
        }

        public bool TryDequeueEvent(
            out WeaponAnimationEvent animationEvent)
        {
            if (pendingEvents.Count == 0)
            {
                animationEvent = default;
                return false;
            }

            animationEvent = pendingEvents.Dequeue();
            return true;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            if (boundRuntime != null)
            {
                boundRuntime.ActionChanged -= OnActionChanged;
                boundRuntime.SwitchChanged -= OnSwitchChanged;
            }

            boundRuntime = null;
            pendingEvents.Clear();
            isDisposed = true;
        }

        private void OnActionChanged(WeaponActionFact fact)
        {
            pendingEvents.Enqueue(new WeaponAnimationEvent(fact));
        }

        private void OnSwitchChanged(WeaponSwitchFact fact)
        {
            pendingEvents.Enqueue(new WeaponAnimationEvent(fact));
        }
    }
}
