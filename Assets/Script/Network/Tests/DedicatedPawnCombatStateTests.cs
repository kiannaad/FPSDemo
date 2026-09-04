using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network052")]
    public sealed class DedicatedPawnCombatStateTests
    {
        [Test]
        public void ApplyEnemyDamage_AndFireConsumption_AdvanceIndependentAuthorityRevisions()
        {
            var state = new DedicatedPawnCombatState(7, 100, 12, 30, "Default");

            DedicatedPawnVitalsResult hit = state.ApplyEnemyDamage(101, 4, 25);
            DedicatedPawnEquipmentResult fire = state.TryConsumeFire(1);
            DedicatedPawnEquipmentResult duplicateFire = state.TryConsumeFire(1);
            DedicatedPawnVitalsResult duplicate = state.ApplyEnemyDamage(101, 4, 25);

            Assert.That(hit.Health, Is.EqualTo(75));
            Assert.That(hit.VitalsRevision, Is.EqualTo(1));
            Assert.That(fire.Accepted, Is.True);
            Assert.That(fire.MagazineAmmo, Is.EqualTo(11));
            Assert.That(fire.EquipmentRevision, Is.EqualTo(1));
            Assert.That(duplicateFire.Accepted, Is.True);
            Assert.That(duplicateFire.IsReplay, Is.True);
            Assert.That(duplicateFire.MagazineAmmo, Is.EqualTo(11));
            Assert.That(duplicateFire.EquipmentRevision, Is.EqualTo(1));
            Assert.That(duplicate.IsReplay, Is.True);
            Assert.That(duplicate.Health, Is.EqualTo(75));
        }
    }
}
