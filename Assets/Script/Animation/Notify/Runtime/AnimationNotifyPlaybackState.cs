using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation
{
    internal sealed class AnimationNotifyPlaybackState
    {
        private const double TimeEpsilon = 0.000001d;

        private enum BoundaryKind
        {
            End,
            Instant,
            Begin,
        }

        private readonly struct Boundary
        {
            public Boundary(double time, BoundaryKind kind, AnimationNotifyRuntimeEntry entry, long cycle)
            {
                Time = time;
                Kind = kind;
                Entry = entry;
                Cycle = cycle;
            }

            public double Time { get; }
            public BoundaryKind Kind { get; }
            public AnimationNotifyRuntimeEntry Entry { get; }
            public long Cycle { get; }
        }

        private sealed class ActiveDuration
        {
            public AnimationNotifyRuntimeEntry Entry;
            public long Cycle;
        }

        private readonly Pawn pawn;
        private readonly AnimationNotifyRuntimeEntry[] entries;
        private readonly double length;
        private readonly bool looping;
        private readonly float speed;
        private readonly AnimationNotifyDispatchQueue dispatchQueue;
        private readonly bool ownsDispatchQueue;
        private AnimationNotifyGeneration generation;
        private readonly List<ActiveDuration> activeDurations = new List<ActiveDuration>();
        private readonly List<Boundary> boundaries = new List<Boundary>();
        private bool hasBaseline;
        private double lastTime;
        private bool isReplaced;
        private bool isCollecting;

        public AnimationNotifyPlaybackState(
            Pawn pawn,
            AnimationNotifyRuntimeEntry[] entries,
            double length,
            bool looping,
            float speed,
            AnimationNotifyDispatchQueue dispatchQueue = null,
            AnimationNotifyGeneration generation = null)
        {
            this.pawn = pawn;
            this.entries = entries ?? Array.Empty<AnimationNotifyRuntimeEntry>();
            this.length = Math.Max(0d, length);
            this.looping = looping;
            this.speed = speed;
            ownsDispatchQueue = dispatchQueue == null;
            this.dispatchQueue = dispatchQueue ?? new AnimationNotifyDispatchQueue();
            this.generation = generation ?? new AnimationNotifyGeneration();
        }

        public int ActiveDurationCount => activeDurations.Count;

        public void ReplaceGeneration(AnimationNotifyGeneration replacement)
        {
            generation = replacement ?? throw new ArgumentNullException(nameof(replacement));
        }

        public void MarkReplaced()
        {
            if (isReplaced)
            {
                return;
            }

            isReplaced = true;
            for (int i = activeDurations.Count - 1; i >= 0; i--)
            {
                if (activeDurations[i].Entry.Notify.FadeOutPolicy == AnimationNotifyFadeOutPolicy.StopImmediately)
                {
                    End(activeDurations[i], AnimationNotifyEndReason.Interrupted);
                }
            }

            DispatchIfOwned();
        }

        public void Restore(double currentTime, float weight)
        {
            isReplaced = false;
            EndAll(AnimationNotifyEndReason.Interrupted);
            lastTime = currentTime;
            hasBaseline = true;
            ActivateDurationsAt(currentTime, weight);
            TickActive(weight);
            DispatchIfOwned();
        }

        public void ResetTime(double currentTime)
        {
            EndAll(AnimationNotifyEndReason.StateStopped);
            lastTime = currentTime;
            hasBaseline = true;
            DispatchIfOwned();
        }

        public void Update(double currentTime, float weight)
        {
            if (isCollecting)
            {
                Debug.LogError("Synchronous Animation Notify collection reentry is not allowed.");
                return;
            }

            isCollecting = true;
            try
            {
                EndBelowWeight(weight);
                if (!hasBaseline)
                {
                    lastTime = currentTime;
                    hasBaseline = true;
                    TriggerStartingPosition(currentTime, weight);
                    TickActive(weight);
                    return;
                }

                BuildBoundaries(lastTime, currentTime);
                for (int i = 0; i < boundaries.Count; i++)
                {
                    Boundary boundary = boundaries[i];
                    if (!CanDispatch(boundary.Entry, weight, boundary.Kind == BoundaryKind.End))
                    {
                        continue;
                    }

                    switch (boundary.Kind)
                    {
                        case BoundaryKind.End:
                            End(FindActive(boundary.Entry, boundary.Cycle), AnimationNotifyEndReason.NaturalEnd);
                            break;
                        case BoundaryKind.Instant:
                            InvokeInstant(boundary.Entry);
                            break;
                        case BoundaryKind.Begin:
                            Begin(boundary.Entry, boundary.Cycle);
                            break;
                    }

                }

                lastTime = currentTime;
                TickActive(weight);
            }
            finally
            {
                boundaries.Clear();
                isCollecting = false;
                DispatchIfOwned();
            }
        }

        public void EndAll(AnimationNotifyEndReason reason)
        {
            for (int i = activeDurations.Count - 1; i >= 0; i--)
            {
                End(activeDurations[i], reason);
            }

            DispatchIfOwned();
        }

        private void TriggerStartingPosition(double currentTime, float weight)
        {
            if (length <= TimeEpsilon)
            {
                return;
            }

            long cycle = ResolveCycle(currentTime);
            double localTime = ResolveLocalTime(currentTime, cycle);
            for (int i = 0; i < entries.Length; i++)
            {
                AnimationNotifyRuntimeEntry entry = entries[i];
                if (!CanDispatch(entry, weight, false))
                {
                    continue;
                }

                if (!entry.IsDuration && Approximately(localTime, entry.StartTime))
                {
                    InvokeInstant(entry);
                }
                else if (entry.IsDuration)
                {
                    bool contains = speed >= 0f
                        ? localTime + TimeEpsilon >= entry.StartTime && localTime < entry.EndTime - TimeEpsilon
                        : localTime > entry.StartTime + TimeEpsilon && localTime <= entry.EndTime + TimeEpsilon;
                    if (contains)
                    {
                        Begin(entry, cycle);
                    }
                }
            }
        }

        private void ActivateDurationsAt(double currentTime, float weight)
        {
            if (length <= TimeEpsilon)
            {
                return;
            }

            long cycle = ResolveCycle(currentTime);
            double localTime = ResolveLocalTime(currentTime, cycle);
            for (int i = 0; i < entries.Length; i++)
            {
                AnimationNotifyRuntimeEntry entry = entries[i];
                if (!entry.IsDuration || !CanDispatch(entry, weight, false))
                {
                    continue;
                }

                bool contains = speed >= 0f
                    ? localTime + TimeEpsilon >= entry.StartTime && localTime < entry.EndTime - TimeEpsilon
                    : localTime > entry.StartTime + TimeEpsilon && localTime <= entry.EndTime + TimeEpsilon;
                if (contains)
                {
                    Begin(entry, cycle);
                }
            }
        }

        private void BuildBoundaries(double previousTime, double currentTime)
        {
            boundaries.Clear();
            if (length <= TimeEpsilon || Approximately(previousTime, currentTime))
            {
                return;
            }

            bool forward = currentTime > previousTime;
            double minimum = Math.Min(previousTime, currentTime);
            double maximum = Math.Max(previousTime, currentTime);
            long firstCycle = looping ? (long)Math.Floor(minimum / length) - 1L : 0L;
            long lastCycle = looping ? (long)Math.Floor(maximum / length) + 1L : 0L;
            for (long cycle = firstCycle; cycle <= lastCycle; cycle++)
            {
                double cycleStart = cycle * length;
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    AnimationNotifyRuntimeEntry entry = entries[entryIndex];
                    if (entry.IsDuration)
                    {
                        AddBoundaryIfCrossed(
                            cycleStart + (forward ? entry.EndTime : entry.StartTime),
                            BoundaryKind.End,
                            entry,
                            cycle,
                            previousTime,
                            currentTime,
                            forward);
                        AddBoundaryIfCrossed(
                            cycleStart + (forward ? entry.StartTime : entry.EndTime),
                            BoundaryKind.Begin,
                            entry,
                            cycle,
                            previousTime,
                            currentTime,
                            forward);
                    }
                    else
                    {
                        AddBoundaryIfCrossed(
                            cycleStart + entry.StartTime,
                            BoundaryKind.Instant,
                            entry,
                            cycle,
                            previousTime,
                            currentTime,
                            forward);
                    }
                }
            }

            boundaries.Sort((left, right) =>
            {
                int timeComparison = left.Time.CompareTo(right.Time);
                if (!forward)
                {
                    timeComparison = -timeComparison;
                }

                if (timeComparison != 0)
                {
                    return timeComparison;
                }

                bool leftEnds = left.Kind == BoundaryKind.End;
                bool rightEnds = right.Kind == BoundaryKind.End;
                if (leftEnds != rightEnds)
                {
                    return leftEnds ? -1 : 1;
                }

                int entryComparison = left.Entry.CompareTo(right.Entry);
                return entryComparison != 0
                    ? entryComparison
                    : left.Kind.CompareTo(right.Kind);
            });
        }

        private void AddBoundaryIfCrossed(
            double time,
            BoundaryKind kind,
            AnimationNotifyRuntimeEntry entry,
            long cycle,
            double previousTime,
            double currentTime,
            bool forward)
        {
            bool crossed = forward
                ? time > previousTime + TimeEpsilon && time <= currentTime + TimeEpsilon
                : time >= currentTime - TimeEpsilon && time < previousTime - TimeEpsilon;
            if (crossed)
            {
                boundaries.Add(new Boundary(time, kind, entry, cycle));
            }
        }

        private bool CanDispatch(AnimationNotifyRuntimeEntry entry, float weight, bool ending)
        {
            if (entry == null || weight + Mathf.Epsilon < entry.MinTriggerWeight)
            {
                return false;
            }

            if (!isReplaced || ending)
            {
                return true;
            }

            return entry.Notify.FadeOutPolicy == AnimationNotifyFadeOutPolicy.ContinueWhileWeighted;
        }

        private void EndBelowWeight(float weight)
        {
            for (int i = activeDurations.Count - 1; i >= 0; i--)
            {
                ActiveDuration active = activeDurations[i];
                if (weight + Mathf.Epsilon < active.Entry.MinTriggerWeight)
                {
                    End(active, AnimationNotifyEndReason.WeightBelowThreshold);
                }
            }
        }

        private void TickActive(float weight)
        {
            for (int i = activeDurations.Count - 1; i >= 0; i--)
            {
                ActiveDuration active = activeDurations[i];
                if (weight + Mathf.Epsilon < active.Entry.MinTriggerWeight)
                {
                    End(active, AnimationNotifyEndReason.WeightBelowThreshold);
                    continue;
                }

                ActiveDuration captured = active;
                dispatchQueue.Enqueue(
                    generation,
                    false,
                    () => ((AnimationDurationNotify)captured.Entry.Notify).OnTick(pawn),
                    exception => End(captured, AnimationNotifyEndReason.Interrupted));
            }
        }

        private void InvokeInstant(AnimationNotifyRuntimeEntry entry)
        {
            dispatchQueue.Enqueue(
                generation,
                false,
                () => ((AnimationInstantNotify)entry.Notify).OnNotify(pawn));
        }

        private void Begin(AnimationNotifyRuntimeEntry entry, long cycle)
        {
            if (FindActive(entry, cycle) != null)
            {
                return;
            }

            ActiveDuration active = new ActiveDuration { Entry = entry, Cycle = cycle };
            activeDurations.Add(active);
            dispatchQueue.Enqueue(
                generation,
                false,
                () => ((AnimationDurationNotify)entry.Notify).OnBegin(pawn),
                exception => activeDurations.Remove(active));
        }

        private void End(ActiveDuration active, AnimationNotifyEndReason reason)
        {
            if (active == null || !activeDurations.Remove(active))
            {
                return;
            }

            dispatchQueue.Enqueue(
                generation,
                true,
                () => ((AnimationDurationNotify)active.Entry.Notify).OnEnd(pawn, reason));
        }

        private ActiveDuration FindActive(AnimationNotifyRuntimeEntry entry, long cycle)
        {
            for (int i = 0; i < activeDurations.Count; i++)
            {
                ActiveDuration active = activeDurations[i];
                if (ReferenceEquals(active.Entry, entry) && active.Cycle == cycle)
                {
                    return active;
                }
            }

            return null;
        }

        private long ResolveCycle(double time)
        {
            if (!looping || length <= TimeEpsilon)
            {
                return 0L;
            }

            if (speed < 0f && Approximately(time % length, 0d) && time > 0d)
            {
                return (long)Math.Floor(time / length) - 1L;
            }

            return (long)Math.Floor(time / length);
        }

        private double ResolveLocalTime(double time, long cycle)
        {
            double local = looping ? time - cycle * length : Math.Max(0d, Math.Min(length, time));
            return Math.Max(0d, Math.Min(length, local));
        }

        private static bool Approximately(double left, double right)
        {
            return Math.Abs(left - right) <= TimeEpsilon;
        }

        private void DispatchIfOwned()
        {
            if (ownsDispatchQueue && !isCollecting && !dispatchQueue.IsDispatching)
            {
                dispatchQueue.Dispatch();
            }
        }
    }
}
