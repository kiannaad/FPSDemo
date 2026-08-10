using System.Collections.Generic;
using UnityEngine;
using CGame.GameplayTags;

namespace CGame
{
    [CreateAssetMenu(fileName = "GameBootstrap", menuName = "CGame/World/Game Bootstrap")]
    public sealed class GameBootstrap : ScriptableObject, IWorldBootstrapFactory,
        IGameSessionBootstrapFactory, IGameplaySceneBootstrap
    {
        [SerializeField] private GameplayTagConfig gameplayTagConfig;
        [SerializeField] private string resourcePackageName = "DefaultPackage";
        [SerializeField] private bool initializeInput = true;
        [SerializeField] private bool initializeResources = true;
        [SerializeField] private CharacterPhysicsSettings characterPhysicsSettings;
        [SerializeField] private GameModeDefinition gameModeDefinition;

        public IReadOnlyList<IWorldCoreService> CreateCoreServices()
        {
            List<IWorldCoreService> services = new List<IWorldCoreService>();
            if (initializeInput)
            {
                services.Add(new InputWorldCoreService());
            }

            if (initializeResources)
            {
                ResourceWorldCoreService resourceService = new ResourceWorldCoreService(resourcePackageName);
                services.Add(resourceService);
                services.Add(new AssetWorldCoreService(resourceService));
            }

            services.Add(new GameplayTagWorldCoreService(gameplayTagConfig));
            return services;
        }

        public GameLauncher CreateLauncher()
        {
            return new GameLauncher(new System.Func<ILaunchStep>[]
            {
                () => new ReadyLaunchStep()
            });
        }

        public ICharacterMotorSimulation CreateCharacterMotorSimulation()
        {
            CharacterPhysicsSettings settings = characterPhysicsSettings;
            bool ownsSettings = settings == null;
            if (settings == null)
            {
                settings = CreateInstance<CharacterPhysicsSettings>();
            }

            return new CharacterPhysicsWorld(settings, ownsSettings);
        }

        public IGameSessionFactory CreateGameSessionFactory()
        {
            return gameModeDefinition == null
                ? null
                : new GameplaySessionFactory(gameModeDefinition);
        }

        public WorldStartResult StartScene(World world, WorldStartResult startResult)
        {
            if (world == null)
            {
                return WorldStartResult.Fail("Gameplay scene bootstrap received no World.");
            }

            if (!(world.CurrentGameSession is GameManager gameManager))
            {
                return WorldStartResult.Fail("Gameplay session did not create a GameManager.");
            }

            GameMode gameMode = gameManager.CurrentGameMode;
            var playerStartRegistry = new PlayerStartRegistry();
            PlayerStart[] playerStarts = FindObjectsOfType<PlayerStart>();
            System.Array.Sort(playerStarts, ComparePlayerStarts);
            foreach (PlayerStart playerStart in playerStarts)
            {
                playerStartRegistry.Register(playerStart.GetInfo());
            }

            if (!gameMode.PrepareLocalPawn(new PawnFactory(), playerStartRegistry))
            {
                return WorldStartResult.Fail(gameMode.LastSpawnFailure);
            }

            return gameMode.CommitLocalPawn()
                ? startResult
                : WorldStartResult.Fail("GameMode could not commit the local Pawn.");
        }

        public bool TryGetPlayerCameraPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!(World.Current?.CurrentGameSession is GameManager gameManager)
                || !(gameManager.CurrentGameMode?.LocalPlayerController?.PlayerCamera
                    is IPlayerCameraRuntime cameraRuntime))
            {
                return false;
            }

            position = cameraRuntime.Position;
            rotation = cameraRuntime.Rotation;
            return true;
        }

        private static int ComparePlayerStarts(PlayerStart left, PlayerStart right)
        {
            int nameComparison = string.CompareOrdinal(left.name, right.name);
            return nameComparison != 0
                ? nameComparison
                : left.GetInstanceID().CompareTo(right.GetInstanceID());
        }
    }
}
