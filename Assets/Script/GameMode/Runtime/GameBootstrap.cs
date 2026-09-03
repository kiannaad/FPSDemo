using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;
using CGame.Ability.Cues;
using CGame.InventoryEquipment;
using CGame.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame
{
    [CreateAssetMenu(fileName = "GameBootstrap", menuName = "CGame/World/Game Bootstrap")]
    public sealed class GameBootstrap : WorldConfiguration, IDedicatedServerBootstrapConfiguration
    {
        [SerializeField] private string resourcePackageName = "DefaultPackage";
        [SerializeField] private bool initializeResources = true;
        [SerializeField] private bool initializeInput = true;
        [SerializeField] private CharacterPhysicsSettings characterPhysicsSettings;
        [SerializeField] private LevelDefinition levelDefinition;
        [SerializeField] private GameModeDefinition gameModeDefinition;
        [SerializeField] private GameplayTagSource[] gameplayTagSources;
        [SerializeField] private WeaponDefinition[] weaponDefinitions;
        [SerializeField] private GameplayCueSet[] gameplayCueSets;
        [SerializeField] private ClientNetworkDefinition clientNetworkDefinition;

        public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems()
        {
            var subSystems = new List<WorldSubSystem>
            {
                new CharacterPhysicsSubSystem(characterPhysicsSettings),
                new GameplayTagWorldCoreService(gameplayTagSources),
                new GameplayCueManager(gameplayCueSets),
                new WeaponCatalogSubSystem(weaponDefinitions)
            };
            if (initializeResources)
            {
                subSystems.Add(new ResourceManager(resourcePackageName));
                subSystems.Add(new AssetManager());
            }

            if (clientNetworkDefinition != null)
            {
                subSystems.Add(new ClientNetworkSubSystem(clientNetworkDefinition));
            }

            return subSystems;
        }

        public override IReadOnlyList<PlayerSubSystem> CreatePlayerSubSystems()
        {
            var subSystems = new List<PlayerSubSystem> { new CursorSubSystem() };
            if (initializeInput)
            {
                subSystems.Add(new InputSubSystem());
            }

            return subSystems;
        }

        public override GameMode CreateGameMode(World world, Player player)
        {
            if (clientNetworkDefinition != null)
            {
                return new NetworkGameMode(
                    world,
                    player,
                    gameModeDefinition.PlayerStateDefinition,
                    world.GetSubSystem<ClientNetworkSubSystem>());
            }

            return gameModeDefinition == null
                ? null
                : gameModeDefinition.CreateGameMode(world, player);
        }

        public override LevelRuntime CreateLevelRuntime()
        {
            return levelDefinition == null ? null : new LevelRuntime(levelDefinition, SceneManager.GetActiveScene());
        }

        public override GameState CreateGameState(World world)
        {
            if (gameModeDefinition == null) return null;
            gameModeDefinition.ValidateRequiredReferences();
            return gameModeDefinition.GameStateDefinition.CreateGameState(
                world,
                gameModeDefinition.ExperienceDefinition);
        }

        public LevelDefinition LevelDefinition => levelDefinition;

        public CharacterPhysicsSettings CharacterPhysicsSettings => characterPhysicsSettings;

        public PawnDefinition PlayerPawnDefinition => gameModeDefinition?.PlayerStateDefinition?.PawnData;

        public DedicatedTargetSpawnDefinition DedicatedTargetSpawnDefinition
        {
            get
            {
                EnemySpawnGameFeatureAction targetAction = gameModeDefinition?.ExperienceDefinition?.GameFeatures
                    .SelectMany(feature => feature.Actions)
                    .OfType<EnemySpawnGameFeatureAction>()
                    .SingleOrDefault();
                if (targetAction?.EnemyDefinition?.PlayerStateDefinition?.PawnPrefab == null)
                    throw new System.InvalidOperationException("Dedicated bootstrap requires one configured Enemy target spawn action.");
                return new DedicatedTargetSpawnDefinition(
                    targetAction.EnemyDefinition.PlayerStateDefinition.PawnPrefab,
                    targetAction.InitialSpawnCount);
            }
        }

        public GameModeDefinition GameModeDefinition => gameModeDefinition;

public void ConfigureGameplayTagSources(params GameplayTagSource[] sources)
        {
            gameplayTagSources = sources ?? System.Array.Empty<GameplayTagSource>();
        }

        public void ConfigureGameMode(GameModeDefinition definition)
        {
            gameModeDefinition = definition ?? throw new System.ArgumentNullException(nameof(definition));
        }

        public void ConfigureGameplayAssembly(LevelDefinition level, GameModeDefinition gameMode)
        {
            levelDefinition = level ?? throw new System.ArgumentNullException(nameof(level));
            gameModeDefinition = gameMode ?? throw new System.ArgumentNullException(nameof(gameMode));
        }

        public void ConfigureWeaponDefinitions(params WeaponDefinition[] definitions)
        {
            weaponDefinitions = definitions ?? System.Array.Empty<WeaponDefinition>();
        }

        public void ConfigureGameplayCueSets(params GameplayCueSet[] cueSets)
        {
            gameplayCueSets = cueSets ?? System.Array.Empty<GameplayCueSet>();
        }

        public void ConfigureClientNetwork(ClientNetworkDefinition definition)
        {
            clientNetworkDefinition = definition;
        }


public void ConfigureResourceInitialization(bool enabled)
        {
            initializeResources = enabled;
        }
}
}
