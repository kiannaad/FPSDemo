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

    }
}
