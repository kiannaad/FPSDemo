using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class GameLauncher
    {
        private readonly IReadOnlyList<Func<ILaunchStep>> stepFactories;
        private readonly List<ActiveStep> activeSteps = new List<ActiveStep>();
        private CancellationTokenSource launchCancellation;
        private GameLaunchResult terminalFailure;
        private long nextAttemptId;

        public GameLauncher(IEnumerable<Func<ILaunchStep>> stepFactories)
        {
            if (stepFactories == null)
            {
                throw new ArgumentNullException(nameof(stepFactories));
            }

            this.stepFactories = new List<Func<ILaunchStep>>(stepFactories);
        }

        public GameLauncherState State { get; private set; } = GameLauncherState.Idle;

        public GameStartRequest? CurrentRequest { get; private set; }

        public async Task<GameLaunchResult> LaunchAsync(CancellationToken cancellationToken = default)
        {
            if (State == GameLauncherState.Failed)
            {
                return terminalFailure;
            }

            if (State != GameLauncherState.Idle)
            {
                throw new InvalidOperationException($"Cannot launch while launcher is {State}.");
            }

            State = GameLauncherState.Launching;
            LaunchContext context = new LaunchContext(new LaunchAttemptId(++nextAttemptId));
            launchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                foreach (Func<ILaunchStep> stepFactory in stepFactories)
                {
                    if (launchCancellation.IsCancellationRequested)
                    {
                        return await FailAsync(
                            context,
                            new LaunchFailure(string.Empty, "Launch was cancelled.", wasCancelled: true));
                    }
                    ILaunchStep step = stepFactory?.Invoke();
                    if (step == null)
                    {
                        return await FailAsync(context, new LaunchFailure(string.Empty, "Launch step factory returned null."));
                    }

                    activeSteps.Add(new ActiveStep(step, context));
                    LaunchStepResult result;
                    try
                    {
                        result = await step.ExecuteAsync(context, launchCancellation.Token);
                    }
                    catch (OperationCanceledException exception)
                    {
                        return await FailAsync(
                            context,
                            new LaunchFailure(step.Name, "Launch was cancelled.", exception, true));
                    }
                    catch (Exception exception)
                    {
                        return await FailAsync(
                            context,
                            new LaunchFailure(step.Name, exception.Message, exception));
                    }

                    if (result == null || !result.Succeeded)
                    {
                        LaunchFailure failure = result?.Failure ??
                                                new LaunchFailure(step.Name, "Launch step returned no result.");
                        if (string.IsNullOrEmpty(failure.StepName))
                        {
                            failure = new LaunchFailure(
                                step.Name,
                                failure.Message,
                                failure.Exception,
                                failure.WasCancelled);
                        }

                        return await FailAsync(context, failure);
                    }
                }

                GameStartRequest request = new GameStartRequest(context.AttemptId);
                CurrentRequest = request;
                State = GameLauncherState.RequestReady;
                return GameLaunchResult.Success(request);
            }
            finally
            {
                launchCancellation?.Dispose();
                launchCancellation = null;
            }
        }

        public async Task<GameLaunchResult> ReturnToLoginAsync(CancellationToken cancellationToken = default)
        {
            if (State != GameLauncherState.RequestReady)
            {
                throw new InvalidOperationException($"Cannot return to login while launcher is {State}.");
            }

            CurrentRequest = null;
            await ExitActiveStepsAsync();
            State = GameLauncherState.Idle;
            return await LaunchAsync(cancellationToken);
        }

        public async Task ShutdownAsync()
        {
            if (State == GameLauncherState.Shutdown)
            {
                return;
            }

            launchCancellation?.Cancel();
            CurrentRequest = null;
            await ExitActiveStepsAsync();
            State = GameLauncherState.Shutdown;
        }

        private async Task<GameLaunchResult> FailAsync(LaunchContext context, LaunchFailure failure)
        {
            CurrentRequest = null;
            await ExitActiveStepsAsync();
            terminalFailure = GameLaunchResult.Fail(failure);
            State = GameLauncherState.Failed;
            return terminalFailure;
        }

        private async Task ExitActiveStepsAsync()
        {
            for (int index = activeSteps.Count - 1; index >= 0; index--)
            {
                ActiveStep activeStep = activeSteps[index];
                try
                {
                    await activeStep.Step.ExitAsync(activeStep.Context);
                }
                catch
                {
                    // Cleanup remains best-effort so every entered step receives ExitAsync.
                }
            }

            activeSteps.Clear();
        }

        private readonly struct ActiveStep
        {
            public ActiveStep(ILaunchStep step, LaunchContext context)
            {
                Step = step;
                Context = context;
            }

            public ILaunchStep Step { get; }

            public LaunchContext Context { get; }
        }
    }
}
