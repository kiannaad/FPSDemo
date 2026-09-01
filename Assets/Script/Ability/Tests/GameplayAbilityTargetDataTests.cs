using System.Collections.Generic;
using CGame.Ability.Cues;
using CGame.Ability.Targeting;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class GameplayAbilityTargetDataTests
    {
        [Test]
        public void EmptyHandle_HasNoEntries()
        {
            Assert.That(GameplayAbilityTargetDataHandle.Empty.Count, Is.Zero);
        }

        [Test]
        public void Constructor_CopiesInputAndPreservesPerEntryHitFacts()
        {
            var firstHit = new GameplayHitResult(
                new Vector3(1f, 2f, 3f),
                Vector3.up,
                traceStart: Vector3.zero,
                traceEnd: Vector3.forward * 10f);
            var secondHit = new GameplayHitResult(
                new Vector3(4f, 5f, 6f),
                Vector3.back,
                traceStart: Vector3.one,
                traceEnd: Vector3.right * 20f);
            var source = new List<SingleTargetHitData>
            {
                new SingleTargetHitData(17UL, 0, firstHit),
                new SingleTargetHitData(17UL, 1, secondHit)
            };

            var handle = new GameplayAbilityTargetDataHandle(source);
            source.Clear();

            Assert.That(handle.Count, Is.EqualTo(2));
            Assert.That(handle[0].ShotId, Is.EqualTo(17UL));
            Assert.That(handle[0].TraceIndex, Is.Zero);
            Assert.That(handle[0].HitResult.Location, Is.EqualTo(firstHit.Location));
            Assert.That(handle[0].HitResult.TraceStart, Is.EqualTo(Vector3.zero));
            Assert.That(handle[1].HitResult.Location, Is.EqualTo(secondHit.Location));
            Assert.That(handle[1].HitResult.TraceEnd, Is.EqualTo(Vector3.right * 20f));
            Assert.That(handle[0].HitReplaced, Is.False);
            Assert.That(handle[1].HitReplaced, Is.False);
        }
    }
}
