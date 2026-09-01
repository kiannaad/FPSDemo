using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CGame.Ability.Cues;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame
{
    public sealed class GameplayCueManager : WorldSubSystem, IGameplayCueRouter
    {
        private readonly Dictionary<GameplayTag, List<Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle>>> routes = new Dictionary<GameplayTag, List<Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle>>>();
        private readonly Dictionary<long, ActiveCue> activeCues = new Dictionary<long, ActiveCue>();
        private readonly GameplayCueSet[] cueSets;
        private long nextHandleValue;

        public Action<string> Log { get; set; }

        public GameplayCueManager(IEnumerable<GameplayCueSet> cueSets = null)
        {
            this.cueSets = cueSets == null ? Array.Empty<GameplayCueSet>() : cueSets.Where(set => set != null).ToArray();
        }

        protected override Task OnInitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegisterConfiguredRoutes();
            GameplayCueRouter.Register(this);
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync()
        {
            RemoveAllActiveCues();
            GameplayCueRouter.Unregister(this);
            routes.Clear();
            return Task.CompletedTask;
        }

        public void RegisterRoute(GameplayTag cueTag, Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle> notify)
        {
            if (cueTag.IsEmpty || !GameplayTagManager.Instance.IsRegistered(cueTag))
            {
                throw new ArgumentException("A registered GameplayCue tag is required.", nameof(cueTag));
            }

            if (notify == null)
            {
                throw new ArgumentNullException(nameof(notify));
            }

            if (!routes.TryGetValue(cueTag, out List<Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle>> notifyList))
            {
                notifyList = new List<Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle>>();
                routes.Add(cueTag, notifyList);
            }

            if (!notifyList.Contains(notify))
            {
                notifyList.Add(notify);
            }
        }

        public void Execute(GameplayTag cueTag, GameplayCueParameters parameters)
        {
            Dispatch(cueTag, GameplayCueEventType.Executed, parameters, default);
        }

        public GameplayCueHandle Add(GameplayTag cueTag, GameplayCueParameters parameters)
        {
            var handle = new GameplayCueHandle(++nextHandleValue);
            activeCues.Add(handle.Value, new ActiveCue(cueTag, parameters, handle));
            Dispatch(cueTag, GameplayCueEventType.OnActive, parameters, handle);
            return handle;
        }

        public bool Remove(GameplayCueHandle handle)
        {
            if (!handle.IsValid || !activeCues.TryGetValue(handle.Value, out ActiveCue activeCue))
            {
                WriteLog($"GameplayCue ignored an invalid Handle {handle.GetHashCode()}.");
                return false;
            }

            activeCues.Remove(handle.Value);
            Dispatch(activeCue.Tag, GameplayCueEventType.Removed, activeCue.Parameters, activeCue.Handle);
            return true;
        }

        public void RemoveForTarget(object target)
        {
            foreach (ActiveCue activeCue in activeCues.Values.Where(candidate => ReferenceEquals(candidate.Parameters.Target, target)).ToArray())
            {
                Remove(activeCue.Handle);
            }
        }

        private void Dispatch(GameplayTag cueTag, GameplayCueEventType eventType, GameplayCueParameters parameters, GameplayCueHandle handle)
        {
            if (cueTag.IsEmpty || !GameplayTagManager.Instance.IsRegistered(cueTag))
            {
                WriteLog($"GameplayCue ignored an unregistered tag '{cueTag}'.");
                return;
            }

            Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle>[] notifications = ResolveRoutes(cueTag).ToArray();
            if (notifications.Length == 0)
            {
                WriteLog($"GameplayCue has no route for '{cueTag}'.");
                return;
            }

            Debug.Log($"[CueDebug] Tag={cueTag}, EventType={eventType}, Target={parameters.Target}, Handle={handle.Value}, NotifyCount={notifications.Length}");

            foreach (Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle> notify in notifications)
            {
                try
                {
                    notify(eventType, parameters, handle);
                }
                catch (Exception exception)
                {
                    WriteLog($"GameplayCue notify failed for '{cueTag}': {exception.Message}");
                }
            }
        }

        private IEnumerable<Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle>> ResolveRoutes(GameplayTag cueTag)
        {
            var notified = new HashSet<Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle>>();
            GameplayTag currentTag = cueTag;
            while (!currentTag.IsEmpty)
            {
                if (routes.TryGetValue(currentTag, out List<Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle>> notifyList))
                {
                    foreach (Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle> notify in notifyList)
                    {
                        if (notified.Add(notify))
                        {
                            yield return notify;
                        }
                    }
                }

                currentTag = GameplayTagManager.Instance.GetParent(currentTag);
            }
        }

        private void RemoveAllActiveCues()
        {
            foreach (ActiveCue activeCue in activeCues.Values.ToArray())
            {
                Remove(activeCue.Handle);
            }
        }

        private void WriteLog(string message)
        {
            Log?.Invoke(message);
        }

        private void RegisterConfiguredRoutes()
        {
            foreach (GameplayCueSet cueSet in cueSets.OrderByDescending(set => set.Priority))
            {
                foreach (GameplayCueSetEntry entry in cueSet.Entries)
                {
                    if (entry == null || entry.CueTag.IsEmpty || !GameplayTagManager.Instance.IsRegistered(entry.CueTag))
                    {
                        WriteLog("GameplayCue skipped an invalid CueSet entry.");
                        continue;
                    }

                    foreach (CueNotifyDefinition notify in entry.Notifies)
                    {
                        if (notify == null)
                        {
                            WriteLog($"GameplayCue skipped a null Notify for '{entry.CueTag}'.");
                            continue;
                        }

                        RegisterRoute(entry.CueTag, notify.HandleCue);
                    }
                }
            }
        }

        private readonly struct ActiveCue
        {
            public ActiveCue(GameplayTag tag, GameplayCueParameters parameters, GameplayCueHandle handle)
            {
                Tag = tag;
                Parameters = parameters;
                Handle = handle;
            }

            public GameplayTag Tag { get; }
            public GameplayCueParameters Parameters { get; }
            public GameplayCueHandle Handle { get; }
        }
    }
}
