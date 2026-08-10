using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Gameplay.PlayMode.Tests
{
    public sealed class GameplaySessionPlayModeTests
    {
        [UnityTest]
        public IEnumerator WorldBehaviour_DrivesSessionControllerAndDestroysOwnershipTree()
        {
            GameObject host = new GameObject("WorldBehaviour.SessionTest");
            WorldBehaviour behaviour = host.AddComponent<WorldBehaviour>();
            PawnData pawnData = PawnData.CreateRuntime(new AbilitySet());
            PlayModeControllerDefinition controllerDefinition =
                ScriptableObject.CreateInstance<PlayModeControllerDefinition>();
            PlayModeGameModeDefinition gameModeDefinition =
                ScriptableObject.CreateInstance<PlayModeGameModeDefinition>();
            gameModeDefinition.Configure(pawnData, controllerDefinition);
            Task<WorldStartResult> startTask = behaviour.StartWorld(
                Array.Empty<IWorldCoreService>(),
                new GameLauncher(new Func<ILaunchStep>[] { () => new ReadyStep() }));
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(startTask.Result.Succeeded, Is.True);
            GameSessionStartResult sessionResult = behaviour.RuntimeWorld.StartGame(
                new GameplaySessionFactory(gameModeDefinition));
            Assert.That(sessionResult.Succeeded, Is.True);
            PlayerController controller = ((GameManager)behaviour.RuntimeWorld.CurrentGameSession)
                .CurrentGameMode.LocalPlayerController;

            yield return null;

            Assert.That(controller.IsActive, Is.True);
            Assert.That(controller.PlayerState.Avatar, Is.Null);
            Assert.That(controller.TickCount, Is.GreaterThanOrEqualTo(1));
            UnityEngine.Object.Destroy(host);
            yield return null;

            Assert.That(controller.IsActive, Is.False);
            Assert.That(World.Current, Is.Null);
            UnityEngine.Object.Destroy(gameModeDefinition);
            UnityEngine.Object.Destroy(controllerDefinition);
            UnityEngine.Object.Destroy(pawnData);
        }

        private sealed class ReadyStep : ILaunchStep
        {
            public string Name => "Ready";

            public Task<LaunchStepResult> ExecuteAsync(
                LaunchContext context,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(LaunchStepResult.Success());
            }

            public Task ExitAsync(LaunchContext context) => Task.CompletedTask;
        }
    }

    public sealed class PlayModeControllerDefinition : ControllerDefinition
    {
        public override PlayerController CreateController(PlayerControllerCreationContext context)
        {
            return PlayerController.Create(context, new DefaultPlayerControllerComponentFactory());
        }
    }

    public sealed class PlayModeGameModeDefinition : GameModeDefinition
    {
        private PawnData pawnData;
        private ControllerDefinition controllerDefinition;

        public void Configure(PawnData pawnData, ControllerDefinition controllerDefinition)
        {
            this.pawnData = pawnData;
            this.controllerDefinition = controllerDefinition;
        }

        public override PawnData ResolvePawnData(GameStartRequest request) => pawnData;

        public override GameMode CreateRuntime(GameModeCreationContext context, PawnData resolvedPawnData)
        {
            return new GameMode(context, controllerDefinition, resolvedPawnData);
        }
    }
}
