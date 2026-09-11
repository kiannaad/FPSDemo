using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class OwnerGameplayTerminalPresenterTests
    {
        private GameObject root;
        private OwnerGameplayTerminalPresenter presenter;
        private int deaths;
        private int exits;
        private CursorLockMode previousLock;
        private bool previousVisible;

        [SetUp]
        public void SetUp()
        {
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            deaths = exits = 0;
            root = new GameObject("OwnerTerminalTest");
            presenter = root.AddComponent<OwnerGameplayTerminalPresenter>();
            presenter.Configure(7, () => deaths++, () => exits++);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        [Test]
        public void Apply_IgnoresOtherOwnersAndStaleVitals_AndKeepsDeathTerminal()
        {
            presenter.Apply(new OwnerGameplayStateEvent { PawnId = 7, VitalsRevision = 3, Health = 60, MaxHealth = 100 });
            presenter.Apply(new OwnerGameplayStateEvent { PawnId = 8, VitalsRevision = 10, Health = 0, MaxHealth = 100, IsDead = true });
            presenter.Apply(new OwnerGameplayStateEvent { PawnId = 7, VitalsRevision = 2, Health = 0, MaxHealth = 100, IsDead = true });
            presenter.RequestExit();
            Assert.That(deaths, Is.Zero);
            Assert.That(exits, Is.Zero);
            var death = new OwnerGameplayStateEvent { PawnId = 7, VitalsRevision = 4, Health = 0, MaxHealth = 100, IsDead = true };
            presenter.Apply(death);
            presenter.Apply(death);
            presenter.Apply(new OwnerGameplayStateEvent { PawnId = 7, VitalsRevision = 5, Health = 100, MaxHealth = 100 });
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(presenter.IsDead, Is.True);
            Assert.That(root.GetComponentsInChildren<Canvas>().Length, Is.EqualTo(1));
            presenter.RequestExit();
            presenter.RequestExit();
            Assert.That(exits, Is.EqualTo(1));
        }

        [TestCase(-1, 100, true)]
        [TestCase(0, 0, true)]
        [TestCase(101, 100, true)]
        [TestCase(50, 100, true)]
        [TestCase(0, 100, false)]
        public void Apply_RejectsInvalidVitals(int health, int maxHealth, bool isDead)
        {
            presenter.Apply(new OwnerGameplayStateEvent { PawnId = 7, VitalsRevision = 0, Health = health, MaxHealth = maxHealth, IsDead = isDead });
            Assert.That(deaths, Is.Zero);
            Assert.That(presenter.IsDead, Is.False);
        }
    }
}
