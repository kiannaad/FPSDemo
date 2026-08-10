using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CGame.WorldRuntime.Tests
{
    public sealed class GameLauncherTests
    {
        [Test]
        public void LaunchAsync_ExecutesStepsInOrderAndProducesRequest()
        {
            List<string> trace = new List<string>();
            GameLauncher launcher = new GameLauncher(new Func<ILaunchStep>[]
            {
                () => new ProbeStep("First", trace),
                () => new ProbeStep("Second", trace)
            });

            GameLaunchResult result = launcher.LaunchAsync().GetAwaiter().GetResult();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Request.LaunchAttemptId.Value, Is.EqualTo(1));
            Assert.That(launcher.State, Is.EqualTo(GameLauncherState.RequestReady));
            Assert.That(trace, Is.EqualTo(new[] { "enter:First:1", "enter:Second:1" }));
        }

        [Test]
        public void FailedStep_ExitsEnteredStepsInReverseAndCannotRetry()
        {
            List<string> trace = new List<string>();
            int factoryCalls = 0;
            GameLauncher launcher = new GameLauncher(new Func<ILaunchStep>[]
            {
                () =>
                {
                    factoryCalls++;
                    return new ProbeStep("First", trace);
                },
                () =>
                {
                    factoryCalls++;
                    return new ProbeStep("Second", trace, LaunchStepResult.Fail("expected"));
                }
            });

            GameLaunchResult first = launcher.LaunchAsync().GetAwaiter().GetResult();
            GameLaunchResult second = launcher.LaunchAsync().GetAwaiter().GetResult();

            Assert.That(first.Succeeded, Is.False);
            Assert.That(second, Is.SameAs(first));
            Assert.That(factoryCalls, Is.EqualTo(2));
            Assert.That(launcher.State, Is.EqualTo(GameLauncherState.Failed));
            Assert.That(trace, Is.EqualTo(new[]
            {
                "enter:First:1",
                "enter:Second:1",
                "exit:Second:1",
                "exit:First:1"
            }));
        }

        [Test]
        public void CancelledStep_BecomesTerminalFailureAndCleansUp()
        {
            List<string> trace = new List<string>();
            CancellationTokenSource cancellation = new CancellationTokenSource();
            GameLauncher launcher = new GameLauncher(new Func<ILaunchStep>[]
            {
                () => new CancellingStep(trace, cancellation)
            });

            GameLaunchResult result = launcher.LaunchAsync(cancellation.Token).GetAwaiter().GetResult();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure.WasCancelled, Is.True);
            Assert.That(trace, Is.EqualTo(new[] { "exit:Cancel:1" }));
        }

        [Test]
        public void ReturnToLogin_ExitsOldAttemptAndCreatesNewAttempt()
        {
            List<string> trace = new List<string>();
            GameLauncher launcher = new GameLauncher(new Func<ILaunchStep>[]
            {
                () => new ProbeStep("Login", trace)
            });

            GameLaunchResult first = launcher.LaunchAsync().GetAwaiter().GetResult();
            GameLaunchResult second = launcher.ReturnToLoginAsync().GetAwaiter().GetResult();

            Assert.That(first.Request.LaunchAttemptId.Value, Is.EqualTo(1));
            Assert.That(second.Request.LaunchAttemptId.Value, Is.EqualTo(2));
            Assert.That(trace, Is.EqualTo(new[]
            {
                "enter:Login:1",
                "exit:Login:1",
                "enter:Login:2"
            }));
        }

        [Test]
        public void ShutdownAsync_IsIdempotentAndExitsOnce()
        {
            List<string> trace = new List<string>();
            GameLauncher launcher = new GameLauncher(new Func<ILaunchStep>[]
            {
                () => new ProbeStep("Login", trace)
            });
            launcher.LaunchAsync().GetAwaiter().GetResult();

            launcher.ShutdownAsync().GetAwaiter().GetResult();
            launcher.ShutdownAsync().GetAwaiter().GetResult();

            Assert.That(launcher.State, Is.EqualTo(GameLauncherState.Shutdown));
            Assert.That(trace, Is.EqualTo(new[] { "enter:Login:1", "exit:Login:1" }));
        }

        private sealed class ProbeStep : ILaunchStep
        {
            private readonly List<string> trace;
            private readonly LaunchStepResult result;

            public ProbeStep(string name, List<string> trace, LaunchStepResult result = null)
            {
                Name = name;
                this.trace = trace;
                this.result = result ?? LaunchStepResult.Success();
            }

            public string Name { get; }

            public Task<LaunchStepResult> ExecuteAsync(LaunchContext context, CancellationToken cancellationToken)
            {
                trace.Add($"enter:{Name}:{context.AttemptId.Value}");
                return Task.FromResult(result);
            }

            public Task ExitAsync(LaunchContext context)
            {
                trace.Add($"exit:{Name}:{context.AttemptId.Value}");
                return Task.CompletedTask;
            }
        }

        private sealed class CancellingStep : ILaunchStep
        {
            private readonly List<string> trace;
            private readonly CancellationTokenSource cancellation;

            public CancellingStep(List<string> trace, CancellationTokenSource cancellation)
            {
                this.trace = trace;
                this.cancellation = cancellation;
            }

            public string Name => "Cancel";

            public Task<LaunchStepResult> ExecuteAsync(LaunchContext context, CancellationToken cancellationToken)
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(LaunchStepResult.Success());
            }

            public Task ExitAsync(LaunchContext context)
            {
                trace.Add($"exit:{Name}:{context.AttemptId.Value}");
                return Task.CompletedTask;
            }
        }
    }
}
