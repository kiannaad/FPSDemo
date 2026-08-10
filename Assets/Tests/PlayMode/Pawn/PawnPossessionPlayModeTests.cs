using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.PawnRuntime.PlayMode.Tests
{
    public sealed class PawnPossessionPlayModeTests
    {
        [UnityTest]
        public IEnumerator PrepareCommitAndRespawn_SwitchesAvatarOnlyAtCommitAndCancelsOldExecution()
        {
            GameplayTagSource tagSource = ScriptableObject.CreateInstance<GameplayTagSource>();
            tagSource.SetDefinition(
                "PawnPossessionPlayModeTests",
                new[] { Node("Ability", false, Node("Pawn", false, Node("Active", true))) });
            GameplayTagRegistryBuildResult tagResult = GameplayTagManager.Instance.Initialize(new[] { tagSource });
            Assert.That(tagResult.Succeeded, Is.True, string.Join(Environment.NewLine, tagResult.Errors));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Pawn.Active");
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            prefab.name = "PawnPrefab";
            prefab.SetActive(false);
            PawnData pawnData = PawnData.CreateRuntime(
                prefab,
                new AbilitySet(new[] { new ActivePawnAbilityDefinition(abilityTag) }));
            PossessionControllerDefinition controllerDefinition =
                ScriptableObject.CreateInstance<PossessionControllerDefinition>();
            PossessionGameModeDefinition gameModeDefinition =
                ScriptableObject.CreateInstance<PossessionGameModeDefinition>();
            gameModeDefinition.Configure(pawnData, controllerDefinition);
            GameObject worldHost = new GameObject("WorldBehaviour.PawnPossessionTest");
            WorldBehaviour behaviour = worldHost.AddComponent<WorldBehaviour>();
            GameObject cameraRoot = new GameObject("VisualEvidenceCamera");
            Camera camera = cameraRoot.AddComponent<Camera>();
            cameraRoot.transform.position = new Vector3(0f, 1f, -5f);
            cameraRoot.transform.LookAt(Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            GameObject lightRoot = new GameObject("VisualEvidenceLight");
            Light light = lightRoot.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightRoot.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            string evidenceDirectory = Environment.GetEnvironmentVariable("CGAME_VISUAL_EVIDENCE_DIR");

            try
            {
                Task<WorldStartResult> startTask = behaviour.StartWorld(
                    Array.Empty<IWorldCoreService>(),
                    new GameLauncher(new Func<ILaunchStep>[] { () => new ReadyStep() }));
                while (!startTask.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(startTask.Result.Succeeded, Is.True);
                Assert.That(behaviour.RuntimeWorld.StartGame(
                    new GameplaySessionFactory(gameModeDefinition)).Succeeded, Is.True);
                GameMode gameMode = ((GameManager)behaviour.RuntimeWorld.CurrentGameSession).CurrentGameMode;
                PlayerController controller = gameMode.LocalPlayerController;
                var starts = new PlayerStartRegistry();
                starts.Register(new PlayerStartInfo("local", Vector3.zero, Quaternion.identity));

                Assert.That(gameMode.PrepareLocalPawn(new PawnFactory(), starts), Is.True);
                PawnAssembly firstCandidate = gameMode.CandidatePawnAssembly;
                Assert.That(controller.PlayerState.CurrentPawn, Is.Null);
                Assert.That(gameMode.CommitLocalPawn(), Is.True);
                yield return null;
                Assert.That(controller.PlayerState.CurrentPawn, Is.SameAs(firstCandidate.Pawn));
                if (!string.IsNullOrWhiteSpace(evidenceDirectory))
                {
                    Directory.CreateDirectory(evidenceDirectory);
                    ScreenCapture.CaptureScreenshot(Path.Combine(
                        evidenceDirectory,
                        "after-first-gameplay-ready.png"));
                    yield return new WaitForEndOfFrame();
                }

                Assert.That(controller.PlayerState.AbilitySystem.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);
                AbilitySpecHandle specHandle = controller.PlayerState.BaseGrantReceipt.SpecHandles[0];
                Assert.That(controller.PlayerState.AbilitySystem.TryGetSpec(specHandle, out AbilitySpec spec), Is.True);
                Assert.That(spec.PrimaryInstance.State, Is.EqualTo(AbilityInstanceState.Active));

                Assert.That(gameMode.PrepareLocalPawn(new PawnFactory(), starts), Is.True);
                PawnAssembly secondCandidate = gameMode.CandidatePawnAssembly;
                Assert.That(controller.PlayerState.CurrentPawn, Is.SameAs(firstCandidate.Pawn));
                Assert.That(firstCandidate.IsDisposed, Is.False);

                Assert.That(gameMode.CommitLocalPawn(), Is.True);
                yield return null;

                Assert.That(controller.PlayerState.CurrentPawn, Is.SameAs(secondCandidate.Pawn));
                Assert.That(controller.ControlledPawn, Is.SameAs(secondCandidate.Pawn));
                Assert.That(spec.PrimaryInstance.State, Is.EqualTo(AbilityInstanceState.Inactive));
                Assert.That(spec.PrimaryInstance.LastEndReason, Is.EqualTo(AbilityEndReason.AvatarChanged));
                Assert.That(controller.PlayerState.AbilitySystem.AbilityCount, Is.EqualTo(1));
                Assert.That(controller.PlayerState.BaseGrantReceipt.IsActive, Is.True);
                Assert.That(firstCandidate.IsDisposed, Is.True);
                Assert.That(firstCandidate.Hero.IsBound, Is.False);
                Assert.That(secondCandidate.Hero.IsBound, Is.True);
                Assert.That(secondCandidate.Root.activeSelf, Is.True);
                if (!string.IsNullOrWhiteSpace(evidenceDirectory))
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(
                        evidenceDirectory,
                        "after-respawn-gameplay-ready.png"));
                    yield return new WaitForEndOfFrame();
                    yield return null;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldHost);
                UnityEngine.Object.DestroyImmediate(cameraRoot);
                UnityEngine.Object.DestroyImmediate(lightRoot);
                GameplayTagManager.Instance.Shutdown();
                UnityEngine.Object.Destroy(gameModeDefinition);
                UnityEngine.Object.Destroy(controllerDefinition);
                UnityEngine.Object.Destroy(pawnData);
                UnityEngine.Object.Destroy(prefab);
                UnityEngine.Object.Destroy(tagSource);
            }
        }

        private static GameplayTagSourceNode Node(
            string segmentName,
            bool isExplicit,
            params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
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

        private sealed class ActivePawnAbilityDefinition : AbilityDefinition
        {
            public ActivePawnAbilityDefinition(GameplayTag abilityTag) : base(abilityTag)
            {
            }

            protected override AbilityInstance CreateInstance() => new ActivePawnAbilityInstance();
        }

        private sealed class ActivePawnAbilityInstance : AbilityInstance
        {
        }
    }

    public sealed class PossessionControllerDefinition : ControllerDefinition
    {
        public override PlayerController CreateController(PlayerControllerCreationContext context)
        {
            return PlayerController.Create(context, new DefaultPlayerControllerComponentFactory());
        }
    }

    public sealed class PossessionGameModeDefinition : GameModeDefinition
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
