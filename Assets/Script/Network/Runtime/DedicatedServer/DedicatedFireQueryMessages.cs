using System;

namespace CGame.Network
{
    [Serializable]
    public sealed class DedicatedFireQueryRequest
    {
        public long MatchId;
        public long PawnId;
        public float OriginX;
        public float OriginY;
        public float OriginZ;
        public float DirectionX;
        public float DirectionY;
        public float DirectionZ;
        public float Range = 1000f;
    }

    [Serializable]
    public sealed class DedicatedFireQueryResult
    {
        public bool Accepted;
        public bool Hit;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float NormalX;
        public float NormalY;
        public float NormalZ;
        public string SurfaceId;
        public string TargetId;
        public long HitEnemyId;
        public long HitPawnId;
        public string Failure;
    }

    [Serializable]
    public sealed class DedicatedEnemyDamageRequest
    {
        public long MatchId;
        public long EnemyId;
        public long CausingPawnId;
        public long CausingShotSequence;
        public int Damage;
    }

    [Serializable]
    public sealed class DedicatedEnemyDamageResult
    {
        public bool Accepted;
        public long EnemyId;
        public int Health;
        public int MaxHealth;
        public long VitalsRevision;
        public bool IsDead;
        public bool DiedThisHit;
        public bool IsReplay;
        public string Failure;
    }

    [Serializable]
    public sealed class DedicatedPawnFireRequest
    {
        public long MatchId;
        public long PawnId;
        public long ClientShotSequence;
    }

    [Serializable]
    public sealed class DedicatedPawnFireResult
    {
        public bool Accepted;
        public int MagazineAmmo;
        public int MagazineCapacity;
        public long EquipmentRevision;
        public string Failure;
    }

    [Serializable]
    public sealed class DedicatedFireCommitRequest
    {
        public long MatchId;
        public long PawnId;
        public long ClientShotSequence;
        public float OriginX;
        public float OriginY;
        public float OriginZ;
        public float DirectionX;
        public float DirectionY;
        public float DirectionZ;
        public float Range;
        public int Damage;
    }

    [Serializable]
    public sealed class DedicatedFireCommitResult
    {
        public bool Accepted;
        public bool IsReplay;
        public int MagazineAmmo;
        public int MagazineCapacity;
        public long EquipmentRevision;
        public bool Hit;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float NormalX;
        public float NormalY;
        public float NormalZ;
        public string SurfaceId;
        public string TargetId;
        public long HitEnemyId;
        public long HitPawnId;
        public DedicatedEnemyDamageResult EnemyDamage;
        public string Failure;
    }
}
