using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedEnemyRosterFactory : IAuthoritativeEnemyRosterFactory
    {
        private readonly LevelRuntime levelRuntime;
        private readonly Transform parent;
        private readonly PawnDefinition motorPawnDefinition;

        public DedicatedEnemyRosterFactory(LevelRuntime levelRuntime, Transform parent = null, PawnDefinition motorPawnDefinition = null)
        {
            this.levelRuntime = levelRuntime ?? throw new ArgumentNullException(nameof(levelRuntime));
            this.parent = parent;
            this.motorPawnDefinition = motorPawnDefinition;
        }

        public IAuthoritativeEnemyRosterReservation Reserve(EnemyRosterEntry entry)
        {
            return new Reservation(entry.EnemyId, levelRuntime.ReserveEnemyPoint(entry.SpawnPointId));
        }

        public IAuthoritativeEnemyEntity Create(EnemyRosterEntry entry, IAuthoritativeEnemyRosterReservation reservation)
        {
            if (!(reservation is Reservation pointReservation) || pointReservation.EnemyId != entry.EnemyId)
                throw new InvalidOperationException("Dedicated enemy reservation is invalid.");

            Transform point = pointReservation.Point;
            GameObject root = motorPawnDefinition?.PawnPrefab != null
                ? UnityEngine.Object.Instantiate(motorPawnDefinition.PawnPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                root.name = $"DedicatedEnemy:{entry.EnemyId}:{entry.ArchetypeId}";
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(point.position, point.rotation);
                root.SetActive(false);
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                CharacterPhysicsMotor motor = root.GetComponent<CharacterPhysicsMotor>();
                if (motor != null)
                {
                    foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
                        behaviour.enabled = behaviour is CharacterPhysicsMotor;
                }
                var entity = root.AddComponent<DedicatedEnemyEntity>();
                entity.Initialize(entry);
                if (motor != null)
                {
                    entity.BindMotorPawn(new Pawn(root, new ActorComponent[] { new PawnMovementComponent(motor) }));
                }
                return new Entity(entry.EnemyId, root);
            }
            catch
            {
                UnityEngine.Object.Destroy(root);
                throw;
            }
        }

        private sealed class Reservation : IAuthoritativeEnemyRosterReservation
        {
            private SpawnPointReservation reservation;

            public Reservation(long enemyId, SpawnPointReservation reservation)
            {
                EnemyId = enemyId;
                this.reservation = reservation ?? throw new ArgumentNullException(nameof(reservation));
            }

            public long EnemyId { get; }
            public Transform Point => reservation?.Transform ?? throw new ObjectDisposedException(nameof(Reservation));
            public void Dispose()
            {
                reservation?.Dispose();
                reservation = null;
            }
        }

        private sealed class Entity : IAuthoritativeEnemyEntity
        {
            private GameObject root;

            public Entity(long enemyId, GameObject root)
            {
                EnemyId = enemyId;
                this.root = root;
            }

            public long EnemyId { get; }
            public void Dispose()
            {
                if (root != null) UnityEngine.Object.Destroy(root);
                root = null;
            }
        }
    }

    public sealed class DedicatedEnemyEntity : MonoBehaviour
    {
        public long EnemyId { get; private set; }
        public string ArchetypeId { get; private set; }
        public int Health { get; private set; } = 100;
        public int MaxHealth { get; private set; } = 100;
        public long VitalsRevision { get; private set; }
        public bool IsDead => Health <= 0;
        public Vector3 PlanarVelocity { get; private set; }
        public Pawn MotorPawn { get; private set; }
        private readonly Dictionary<string, DedicatedDamageResult> damageResultsByCause =
            new Dictionary<string, DedicatedDamageResult>(StringComparer.Ordinal);
        public void Initialize(EnemyRosterEntry entry)
        {
            if (EnemyId != 0) throw new InvalidOperationException("Dedicated enemy entity is already initialized.");
            EnemyId = entry.EnemyId;
            ArchetypeId = entry.ArchetypeId;
        }

        public void BindMotorPawn(Pawn pawn)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            if (MotorPawn != null) throw new InvalidOperationException("Dedicated enemy motor Pawn is already bound.");
            MotorPawn = pawn;
        }

        public void ApplyMotorState(DedicatedEnemyMotorState state)
        {
            PlanarVelocity = state.PlanarVelocity;
        }

        public DedicatedDamageResult ApplyDamage(long causingPawnId, long causingShotSequence, int damage)
        {
            if (causingPawnId <= 0) throw new ArgumentOutOfRangeException(nameof(causingPawnId));
            if (causingShotSequence <= 0) throw new ArgumentOutOfRangeException(nameof(causingShotSequence));
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));

            string cause = $"{causingPawnId}:{causingShotSequence}";
            if (damageResultsByCause.TryGetValue(cause, out DedicatedDamageResult replay))
                return replay.WithReplay();

            int previousHealth = Health;
            Health = Math.Max(0, Health - damage);
            if (Health != previousHealth) VitalsRevision = checked(VitalsRevision + 1);
            var result = new DedicatedDamageResult(
                EnemyId,
                Health,
                MaxHealth,
                VitalsRevision,
                IsDead,
                previousHealth > 0 && IsDead,
                false);
            damageResultsByCause.Add(cause, result);
            return result;
        }
    }

    public readonly struct DedicatedDamageResult
    {
        public DedicatedDamageResult(long enemyId, int health, int maxHealth, long vitalsRevision, bool isDead, bool diedThisHit, bool isReplay)
        {
            EnemyId = enemyId;
            Health = health;
            MaxHealth = maxHealth;
            VitalsRevision = vitalsRevision;
            IsDead = isDead;
            DiedThisHit = diedThisHit;
            IsReplay = isReplay;
        }

        public long EnemyId { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public long VitalsRevision { get; }
        public bool IsDead { get; }
        public bool DiedThisHit { get; }
        public bool IsReplay { get; }

        public EnemyActionKind? PresentationActionKind => IsReplay || IsDead && !DiedThisHit
            ? null
            : DiedThisHit ? EnemyActionKind.Death : EnemyActionKind.Hit;

        public DedicatedDamageResult WithReplay() => new DedicatedDamageResult(
            EnemyId, Health, MaxHealth, VitalsRevision, IsDead, DiedThisHit, true);
    }
}
