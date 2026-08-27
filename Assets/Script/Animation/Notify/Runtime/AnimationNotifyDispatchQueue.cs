using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation
{
    internal sealed class AnimationNotifyDispatchQueue
    {
        private const int MaxDispatchesPerPass = 64;

        private sealed class PendingInvocation
        {
            public AnimationNotifyGeneration Generation;
            public bool AllowInvalidGeneration;
            public Action Callback;
            public Action<Exception> FailureCallback;
        }

        private readonly List<PendingInvocation> pending = new List<PendingInvocation>();
        private readonly HashSet<AnimationNotifyGeneration> retireAfterDispatch =
            new HashSet<AnimationNotifyGeneration>();
        private bool isDispatching;

        public int PendingCount => pending.Count;

        public bool IsDispatching => isDispatching;

        public void Enqueue(
            AnimationNotifyGeneration generation,
            bool allowInvalidGeneration,
            Action callback,
            Action<Exception> failureCallback = null)
        {
            if (generation == null || callback == null)
            {
                return;
            }

            pending.Add(new PendingInvocation
            {
                Generation = generation,
                AllowInvalidGeneration = allowInvalidGeneration,
                Callback = callback,
                FailureCallback = failureCallback
            });
        }

        public void RetireAfterCurrentDispatch(AnimationNotifyGeneration generation)
        {
            if (generation != null)
            {
                retireAfterDispatch.Add(generation);
            }
        }

        public void Dispatch()
        {
            if (isDispatching)
            {
                Debug.LogError("Recursive Animation Notify dispatch is not allowed.");
                return;
            }

            isDispatching = true;
            int dispatchCount = 0;
            try
            {
                for (int index = 0; index < pending.Count; index++)
                {
                    PendingInvocation invocation = pending[index];
                    if (!invocation.AllowInvalidGeneration && !invocation.Generation.IsValid)
                    {
                        continue;
                    }

                    if (dispatchCount >= MaxDispatchesPerPass)
                    {
                        Debug.LogError(
                            $"Animation Notify dispatch exceeded {MaxDispatchesPerPass} callbacks in one update.");
                        break;
                    }

                    try
                    {
                        invocation.Callback();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        invocation.FailureCallback?.Invoke(exception);
                    }

                    dispatchCount++;
                }
            }
            finally
            {
                pending.Clear();
                foreach (AnimationNotifyGeneration generation in retireAfterDispatch)
                {
                    generation.Invalidate();
                }

                retireAfterDispatch.Clear();
                isDispatching = false;
            }
        }
    }
}
