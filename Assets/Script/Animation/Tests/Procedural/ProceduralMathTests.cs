using NUnit.Framework;
using UnityEngine;

namespace CGame.Animation.Tests
{
    public sealed class ProceduralMathTests
    {
        [Test]
        public void VectorCurve_UsesLongestAxisAndEvaluatesEachAxis()
        {
            var curve = new VectorCurve(
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                AnimationCurve.Linear(0f, 0f, 2f, 4f),
                AnimationCurve.Constant(0f, 0.5f, 3f));

            Assert.That(curve.IsValid, Is.True);
            Assert.That(curve.Length, Is.EqualTo(2f));
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(new Vector3(0.5f, 1f, 3f)));
        }

        [TestCase(EaseMode.Linear, 0.25f, 0.25f)]
        [TestCase(EaseMode.EaseIn, 0.5f, 0.25f)]
        [TestCase(EaseMode.EaseOut, 0.5f, 0.75f)]
        [TestCase(EaseMode.EaseInOut, 0.25f, 0.125f)]
        [TestCase(EaseMode.EaseInOut, 0.75f, 0.875f)]
        public void EvaluateEase_IsDeterministic(EaseMode mode, float input, float expected)
        {
            Assert.That(KCurves.EvaluateEase(input, mode),
                Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void Spring_AdvancesDeterministicallyAndResetClearsState()
        {
            VectorSpring spring = VectorSpring.Identity;
            VectorSpringState firstState = default;
            VectorSpringState secondState = default;
            Vector3 first = Vector3.zero;
            Vector3 second = Vector3.zero;

            for (int index = 0; index < 4; index++)
            {
                first = KSpringMath.Interpolate(
                    first, Vector3.one, spring, ref firstState, 0.1f);
                second = KSpringMath.Interpolate(
                    second, Vector3.one, spring, ref secondState, 0.1f);
            }

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.x, Is.GreaterThan(0f));
            firstState.Reset();
            Assert.That(firstState.X.Velocity, Is.Zero);
            Assert.That(firstState.Y.Error, Is.Zero);
            Assert.That(firstState.Z.Velocity, Is.Zero);
        }
    }
}
