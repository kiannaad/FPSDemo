using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network052")]
    public sealed class OwnerGameplayHudStateTests
    {
        [Test]
        public void Apply_RequiresLocalPossessionAndBothMonotonicAuthorityRevisions()
        {
            var state = new OwnerGameplayHudState();

            Assert.That(state.Apply(new OwnerGameplayVitals(8, 1, 100, 100, false), new OwnerGameplayEquipment(8, 1, "Rifle", 12, 30), false), Is.True);
            Assert.That(state.Visible, Is.False);

            Assert.That(state.Apply(new OwnerGameplayVitals(8, 1, 80, 100, false), new OwnerGameplayEquipment(8, 1, "Rifle", 12, 30), true), Is.True);
            Assert.That(state.Text, Is.EqualTo("Rifle  HP 100/100  Ammo 12/30"));
            Assert.That(state.Apply(new OwnerGameplayVitals(8, 1, 10, 100, false), new OwnerGameplayEquipment(8, 2, "Rifle", 11, 30), true), Is.True);
            Assert.That(state.Text, Is.EqualTo("Rifle  HP 100/100  Ammo 11/30"));
            Assert.That(state.Apply(new OwnerGameplayVitals(8, 2, 10, 100, false), new OwnerGameplayEquipment(8, 2, "Rifle", 11, 30), true), Is.True);
            Assert.That(state.Text, Is.EqualTo("Rifle  HP 10/100  Ammo 11/30"));
        }

        [Test]
        public void Apply_DeadOwnerState_RetainsTheFinalAuthoritativeVitals()
        {
            var state = new OwnerGameplayHudState();

            state.Apply(new OwnerGameplayVitals(8, 1, 80, 100, false), new OwnerGameplayEquipment(8, 1, "Rifle", 12, 30), true);
            Assert.That(state.Apply(new OwnerGameplayVitals(8, 2, 0, 100, true), new OwnerGameplayEquipment(8, 1, "Rifle", 12, 30), true), Is.True);
            Assert.That(state.Visible, Is.True);
            Assert.That(state.Text, Is.EqualTo("Rifle  HP 0/100  Ammo 12/30"));
        }

        [Test]
        public void Reset_AllowsTheNextMatchToStartAgainAtRevisionZero()
        {
            var state = new OwnerGameplayHudState();
            state.Apply(new OwnerGameplayVitals(8, 20, 0, 100, true), new OwnerGameplayEquipment(8, 11, "Rifle", 12, 30), true);

            state.Reset();
            Assert.That(state.Apply(new OwnerGameplayVitals(8, 0, 100, 100, false), new OwnerGameplayEquipment(8, 0, "Default", 12, 30), true), Is.True);
            Assert.That(state.Text, Is.EqualTo("Default  HP 100/100  Ammo 12/30"));
        }
    }
}
