using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CGame.CharacterRuntime.Tests
{
    public sealed class CharacterRuntimeBoundaryTests
    {
        [Test]
        public void Pawn_ConsumesJumpOnceAndKeepsContinuousIntent()
        {
            Pawn pawn = new Pawn();
            pawn.SubmitControlIntent(new CharacterControlIntent(new Vector3(2f, 0f, 0f), true, true));

            CharacterMovementCommand first = pawn.ConsumeMovementCommand();
            CharacterMovementCommand second = pawn.ConsumeMovementCommand();

            Assert.That(first.MovementInput, Is.EqualTo(Vector3.right));
            Assert.That(first.JumpRequested, Is.True);
            Assert.That(first.SprintRequested, Is.True);
            Assert.That(second.MovementInput, Is.EqualTo(Vector3.right));
            Assert.That(second.JumpRequested, Is.False);
            Assert.That(second.SprintRequested, Is.True);
        }

        [Test]
        public void Controller_PossessionRemainsSymmetric()
        {
            Controller controller = new Controller();
            Pawn first = new Pawn();
            Pawn second = new Pawn();

            controller.PossessingPawn(first);
            controller.PossessingPawn(second);

            Assert.That(first.Controller, Is.Null);
            Assert.That(second.Controller, Is.SameAs(controller));
            Assert.That(controller.ControlledPawn, Is.SameAs(second));

            controller.UnpossessingPawn();

            Assert.That(second.Controller, Is.Null);
            Assert.That(controller.ControlledPawn, Is.Null);
        }

        [Test]
        public void Pawn_UpdatesComponentsByDescendingPriorityAndShutsThemDown()
        {
            List<string> trace = new List<string>();
            Pawn pawn = new Pawn();
            RecordingComponent low = new RecordingComponent("low", 1, trace);
            RecordingComponent high = new RecordingComponent("high", 10, trace);

            pawn.RegisteringComponent(low);
            pawn.RegisteringComponent(high);
            trace.Clear();
            pawn.UpdatingPawn(0.25f);
            pawn.ShuttingDownPawn();

            Assert.That(trace, Is.EqualTo(new[] { "high:update", "low:update", "high:shutdown", "low:shutdown" }));
        }

        [Test]
        public void GameplayRecoilState_RecoversWithoutOvershooting()
        {
            GameplayRecoilState state = new GameplayRecoilState();
            state.ApplyKick(new Vector2(2f, 0f), 4f);

            Vector2 firstDelta = state.Advance(0.25f);
            Vector2 secondDelta = state.Advance(1f);

            Assert.That(firstDelta, Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(secondDelta, Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(state.Offset, Is.EqualTo(Vector2.zero));
        }

        private sealed class RecordingComponent : IComponent
        {
            private readonly string name;
            private readonly List<string> trace;

            public RecordingComponent(string name, int priority, List<string> trace)
            {
                this.name = name;
                Priority = priority;
                this.trace = trace;
            }

            public int Priority { get; }

            public void InitializingComponent(Pawn owner)
            {
                trace.Add($"{name}:initialize");
            }

            public void UpdatingComponent(float elapseSeconds)
            {
                trace.Add($"{name}:update");
            }

            public void FixedUpdatingComponent(float elapseSeconds)
            {
            }

            public void LateUpdatingComponent(float elapseSeconds)
            {
            }

            public void ShuttingDownComponent()
            {
                trace.Add($"{name}:shutdown");
            }
        }
    }
}
