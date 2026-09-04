using System;

namespace CGame.Network
{
    [Serializable]
    public sealed class DedicatedServerHealthSnapshot
    {
        public string status;
        public long matchId;
        public int dataPort;
        public int healthPort;
        public string levelId;
        public string contentVersion;
        public int authorityPawnCount;
        public long fixedStepCount;
        public string[] targetIds;
        public DedicatedEnemyHealthState[] enemySpawns;
        public DedicatedEnemyActionState[] enemyActions;
        public DedicatedPawnHealthState[] pawnStates;
        public string failure;
    }

    [Serializable]
    public sealed class DedicatedPawnHealthState
    {
        public long pawnId;
        public long vitalsRevision;
        public int health;
        public int maxHealth;
        public bool isDead;
        public long equipmentRevision;
        public string weaponName;
        public int magazineAmmo;
        public int magazineCapacity;
    }

    [Serializable]
    public sealed class DedicatedEnemyHealthState
    {
        public long enemyId;
        public string archetypeId;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW;
        public float velocityX;
        public float velocityZ;
        public int health;
    }

    [Serializable]
    public sealed class DedicatedEnemyActionState
    {
        public long enemyId;
        public long actionSequence;
        public string actionKind;
        public long serverTick;
        public long vitalsRevision;
    }
}
