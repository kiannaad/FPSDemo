using System;

namespace CGame.Animation
{
    public sealed class CharacterWeaponPresentationReplacement : IDisposable
    {
        private CharacterWeaponPresentationController owner;

        internal CharacterWeaponPresentationReplacement(
            CharacterWeaponPresentationController owner,
            WeaponPresentationInstance expectedCurrent,
            WeaponPresentationInstance candidate,
            WeaponAnimationDefinition definition,
            uint generation)
        {
            this.owner = owner;
            ExpectedCurrent = expectedCurrent;
            Candidate = candidate;
            Definition = definition;
            Generation = generation;
        }

        internal WeaponPresentationInstance ExpectedCurrent { get; }
        internal WeaponAnimationDefinition Definition { get; }
        internal uint Generation { get; }
        public WeaponPresentationInstance Candidate { get; private set; }
        public bool IsValid => owner != null && Candidate != null;

        public void Commit()
        {
            CharacterWeaponPresentationController currentOwner = owner;
            if (currentOwner == null || !currentOwner.TryCommitReplacement(this))
            {
                throw new InvalidOperationException(
                    "The weapon presentation replacement is no longer valid.");
            }
        }

        public void Dispose()
        {
            CharacterWeaponPresentationController currentOwner = owner;
            if (currentOwner != null)
            {
                currentOwner.RollbackReplacement(this);
            }
        }

        internal void MarkFinalized()
        {
            owner = null;
            Candidate = null;
        }
    }
}
