using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network041")]
    [Category("Network042")]
    public sealed class NetworkAnimationActionBridgeTests
    {
        [Test]
        public void ApplyStarted_WithSameActionSequence_PlaysOnlyOnceAtServerPhase()
        {
            var clock = new NetworkTickClock();
            clock.Observe(180);
            var presenter = new RecordingActionPresenter();
            var bridge = new NetworkAnimationActionBridge(7, 3, clock, presenter);
            NetworkAnimationActionStarted started = Started(11, 3, 150, 120);

            bridge.ApplyStarted(started);
            bridge.ApplyStarted(started);

            Assert.That(bridge.PlayedActionCount, Is.EqualTo(1));
            Assert.That(bridge.LastElapsedTicks, Is.EqualTo(30));
            Assert.That(presenter.LastElapsedSeconds, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void ApplyStarted_RejectsOldRevisionAndElapsedAction()
        {
            var clock = new NetworkTickClock();
            clock.Observe(300);
            var presenter = new RecordingActionPresenter();
            var bridge = new NetworkAnimationActionBridge(7, 3, clock, presenter);

            bridge.ApplyStarted(Started(12, 2, 290, 120));
            bridge.ApplyStarted(Started(13, 3, 100, 120));

            Assert.That(bridge.PlayedActionCount, Is.Zero);
            Assert.That(bridge.RejectedActionCount, Is.EqualTo(2));
        }

        [Test]
        public void ApplyTerminal_StopsOnlyMatchingSequenceAndLateStartDoesNotPlay()
        {
            var clock = new NetworkTickClock();
            clock.Observe(180);
            var presenter = new RecordingActionPresenter();
            var bridge = new NetworkAnimationActionBridge(7, 3, clock, presenter);

            bridge.ApplyStarted(Started(20, 3, 170, 120));
            bridge.ApplyTerminal(new NetworkAnimationActionTerminal
            {
                PawnId = 7,
                PossessionRevision = 3,
                ActionSequence = 21,
                ServerTick = 181,
                TerminalKind = NetworkAnimationActionTerminalKind.Cancelled
            });
            bridge.ApplyTerminal(new NetworkAnimationActionTerminal
            {
                PawnId = 7,
                PossessionRevision = 3,
                ActionSequence = 20,
                ServerTick = 182,
                TerminalKind = NetworkAnimationActionTerminalKind.Cancelled
            });
            bridge.ApplyStarted(Started(21, 3, 175, 120));

            Assert.That(presenter.StopCount, Is.EqualTo(1));
            Assert.That(bridge.ActiveActionCount, Is.Zero);
            Assert.That(bridge.PlayedActionCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyStarted_MissingPresentationIsNoOp()
        {
            var clock = new NetworkTickClock();
            clock.Observe(180);
            var presenter = new RecordingActionPresenter { Available = false };
            var bridge = new NetworkAnimationActionBridge(7, 3, clock, presenter);

            bridge.ApplyStarted(Started(30, 3, 170, 120));

            Assert.That(bridge.PlayedActionCount, Is.Zero);
            Assert.That(bridge.PresentationUnavailableCount, Is.EqualTo(1));
            Assert.That(bridge.ActiveActionCount, Is.Zero);
        }

        [Test]
        [Category("Network042")]
        public void ApplyStarted_WithPredictionNonce_ConfirmsWithoutReplay()
        {
            var clock = new NetworkTickClock();
            clock.Observe(180);
            var presenter = new RecordingActionPresenter();
            var bridge = new NetworkAnimationActionBridge(7, 3, clock, presenter);
            long nonce = bridge.BeginPredicted(NetworkAnimationActionKind.Melee, "knife", 9);
            NetworkAnimationActionStarted started = Started(40, 3, 180, 60);
            started.PredictionNonce = nonce;
            started.ActionKind = NetworkAnimationActionKind.Melee;

            bridge.ApplyStarted(started);

            Assert.That(presenter.PlayCount, Is.EqualTo(1));
            Assert.That(bridge.ConfirmedPredictionCount, Is.EqualTo(1));
            Assert.That(bridge.ActiveActionCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Network042")]
        public void ApplyTerminal_CommitKeepsPlaybackUntilEnded()
        {
            var clock = new NetworkTickClock();
            clock.Observe(180);
            var presenter = new RecordingActionPresenter();
            var bridge = new NetworkAnimationActionBridge(7, 3, clock, presenter);
            bridge.ApplyStarted(Started(41, 3, 180, 120));

            bool committed = bridge.ApplyTerminal(Terminal(41, NetworkAnimationActionTerminalKind.Committed));

            Assert.That(committed, Is.True);
            Assert.That(presenter.StopCount, Is.Zero);
            Assert.That(bridge.ActiveActionCount, Is.EqualTo(1));

            bool ended = bridge.ApplyTerminal(Terminal(41, NetworkAnimationActionTerminalKind.Ended));

            Assert.That(ended, Is.True);
            Assert.That(presenter.StopCount, Is.EqualTo(1));
            Assert.That(bridge.ActiveActionCount, Is.Zero);
        }

        private static NetworkAnimationActionTerminal Terminal(
            long sequence,
            NetworkAnimationActionTerminalKind terminalKind) => new NetworkAnimationActionTerminal
        {
            PawnId = 7,
            PossessionRevision = 3,
            ActionSequence = sequence,
            ServerTick = 181,
            TerminalKind = terminalKind
        };

        private static NetworkAnimationActionStarted Started(
            long sequence,
            long revision,
            long startTick,
            int durationTicks) => new NetworkAnimationActionStarted
        {
            PawnId = 7,
            PossessionRevision = revision,
            ActionSequence = sequence,
            ServerStartTick = startTick,
            DurationTicks = durationTicks,
            ActionKind = NetworkAnimationActionKind.Reload,
            VariantId = "ak12-reload",
            EquipmentInstanceId = 9
        };

        private sealed class RecordingActionPresenter : INetworkAnimationActionPresenter
        {
            private readonly object handle = new object();

            public bool Available { get; set; } = true;
            public float LastElapsedSeconds { get; private set; }
            public int StopCount { get; private set; }
            public int PlayCount { get; private set; }

            public bool TryPlay(NetworkAnimationActionStarted action, float elapsedSeconds, out object playbackHandle)
            {
                LastElapsedSeconds = elapsedSeconds;
                PlayCount++;
                playbackHandle = Available ? handle : null;
                return Available;
            }

            public void Stop(object playbackHandle)
            {
                if (ReferenceEquals(playbackHandle, handle)) StopCount++;
            }
        }
    }
}
