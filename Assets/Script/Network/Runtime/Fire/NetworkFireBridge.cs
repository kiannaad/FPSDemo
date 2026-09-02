using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public sealed class NetworkFireBridge
    {
        private readonly long ownerPawnId;
        private readonly long ownerPossessionRevision;
        private readonly Dictionary<long, FireRequest> pendingRequestsByNonce = new Dictionary<long, FireRequest>();
        private readonly HashSet<FireSequenceKey> observedShotSequences = new HashSet<FireSequenceKey>();
        private long nextPredictionNonce;
        private long nextClientShotSequence;

        public NetworkFireBridge(long ownerPawnId, long ownerPossessionRevision)
        {
            if (ownerPawnId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerPawnId));
            if (ownerPossessionRevision < 0) throw new ArgumentOutOfRangeException(nameof(ownerPossessionRevision));
            this.ownerPawnId = ownerPawnId;
            this.ownerPossessionRevision = ownerPossessionRevision;
        }

        public int AuthoritativeMagazineAmmo { get; private set; } = -1;
        public event Action<FireRequest> OwnerPredictionCreated;
        public event Action<FireCommitted> OwnerPredictionConfirmed;
        public event Action<FireRejected> OwnerPredictionRejected;
        public event Action<FireCommitted> RemoteFireCommitted;

        public FireRequest BeginPrediction(
            long equipmentInstanceId,
            int predictedMagazineAmmo,
            long clientFireTick = 0,
            float originX = 0f,
            float originY = 0f,
            float originZ = 0f,
            float directionX = 0f,
            float directionY = 0f,
            float directionZ = 1f)
        {
            if (equipmentInstanceId <= 0) throw new ArgumentOutOfRangeException(nameof(equipmentInstanceId));
            if (predictedMagazineAmmo < 0) throw new ArgumentOutOfRangeException(nameof(predictedMagazineAmmo));

            var request = new FireRequest
            {
                PawnId = ownerPawnId,
                PossessionRevision = ownerPossessionRevision,
                PredictionNonce = checked(++nextPredictionNonce),
                ClientShotSequence = checked(++nextClientShotSequence),
                EquipmentInstanceId = equipmentInstanceId,
                ClientFireTick = clientFireTick,
                OriginX = originX,
                OriginY = originY,
                OriginZ = originZ,
                DirectionX = directionX,
                DirectionY = directionY,
                DirectionZ = directionZ
            };
            pendingRequestsByNonce.Add(request.PredictionNonce, request);
            OwnerPredictionCreated?.Invoke(request);
            return request;
        }

        public bool ApplyCommitted(FireCommitted committed)
        {
            if (!IsValid(committed)) return false;
            if (!observedShotSequences.Add(new FireSequenceKey(
                    committed.PawnId,
                    committed.PossessionRevision,
                    committed.ShotSequence)))
                return false;

            if (committed.PawnId == ownerPawnId && committed.PossessionRevision == ownerPossessionRevision &&
                pendingRequestsByNonce.TryGetValue(committed.PredictionNonce, out FireRequest pending) &&
                pending.ClientShotSequence == committed.ClientShotSequence)
            {
                pendingRequestsByNonce.Remove(committed.PredictionNonce);
                AuthoritativeMagazineAmmo = committed.AuthoritativeMagazineAmmo;
                OwnerPredictionConfirmed?.Invoke(committed);
                return true;
            }

            RemoteFireCommitted?.Invoke(committed);
            return true;
        }

        public bool ApplyRejected(FireRejected rejected)
        {
            if (rejected == null || rejected.PawnId != ownerPawnId ||
                rejected.PossessionRevision != ownerPossessionRevision || rejected.PredictionNonce <= 0 ||
                !pendingRequestsByNonce.TryGetValue(rejected.PredictionNonce, out FireRequest pending) ||
                pending.ClientShotSequence != rejected.ClientShotSequence)
                return false;

            pendingRequestsByNonce.Remove(rejected.PredictionNonce);
            AuthoritativeMagazineAmmo = rejected.AuthoritativeMagazineAmmo;
            OwnerPredictionRejected?.Invoke(rejected);
            return true;
        }

        private static bool IsValid(FireCommitted committed) =>
            committed != null && committed.PawnId > 0 && committed.PossessionRevision >= 0 &&
            committed.ShotSequence > 0 && committed.AuthoritativeMagazineAmmo >= 0;

        private readonly struct FireSequenceKey : IEquatable<FireSequenceKey>
        {
            private readonly long pawnId;
            private readonly long possessionRevision;
            private readonly long shotSequence;

            public FireSequenceKey(long pawnId, long possessionRevision, long shotSequence)
            {
                this.pawnId = pawnId;
                this.possessionRevision = possessionRevision;
                this.shotSequence = shotSequence;
            }

            public bool Equals(FireSequenceKey other) => pawnId == other.pawnId &&
                                                        possessionRevision == other.possessionRevision &&
                                                        shotSequence == other.shotSequence;

            public override bool Equals(object obj) => obj is FireSequenceKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(pawnId, possessionRevision, shotSequence);
        }
    }
}
