using System;
using System.Collections.Generic;

namespace CGame.Animation
{
    public sealed class DirectWeaponPresentationLoader : IWeaponPresentationLoader
    {
        private readonly List<WeaponAnimationDefinition> definitions = new List<WeaponAnimationDefinition>();

        public DirectWeaponPresentationLoader(IEnumerable<WeaponAnimationDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (WeaponAnimationDefinition definition in definitions)
            {
                if (definition != null)
                {
                    this.definitions.Add(definition);
                }
            }
        }

        public IDisposable BeginLoad(
            WeaponId weaponId,
            WeaponPresentationLoadTicket ticket,
            Action<WeaponPresentationLoadTicket, WeaponPresentationLoadResult> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));

            WeaponAnimationDefinition definition = FindDefinition(weaponId);
            string definitionId = definition != null ? definition.name : weaponId.ToString();
            var lease = new WeaponPresentationResourceLease(definition, definitionId);
            completed(ticket, new WeaponPresentationLoadResult(lease, FindMissingField(definition)));
            return CompletedLoad.Instance;
        }

        private WeaponAnimationDefinition FindDefinition(WeaponId weaponId)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].WeaponId == weaponId)
                {
                    return definitions[i];
                }
            }

            return null;
        }

        private static string FindMissingField(WeaponAnimationDefinition definition)
        {
            if (definition == null) return "Definition";
            if (definition.Idle == null || !definition.Idle.IsValid) return "Idle";
            bool hasMove = (definition.Walk != null && definition.Walk.IsValid)
                || (definition.Run != null && definition.Run.IsValid);
            if (!hasMove) return "WalkOrRun";
            if (definition.Fire == null || !definition.Fire.IsValid) return "Fire";
            if (definition.WeaponModelFire == null || !definition.WeaponModelFire.IsValid) return "WeaponModelFire";
            if (definition.Reload == null || !definition.Reload.IsValid) return "Reload";
            if (definition.WeaponModelReload == null || !definition.WeaponModelReload.IsValid) return "WeaponModelReload";
            if (definition.PresentationPrefab == null) return "PresentationPrefab";
            return null;
        }

        private sealed class CompletedLoad : IDisposable
        {
            public static readonly CompletedLoad Instance = new CompletedLoad();

            public void Dispose()
            {
            }
        }
    }
}
