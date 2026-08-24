using System.Collections.Generic;
using CGame.GameplayTags;
using CGame.InventoryEquipment;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "GameBootstrap", menuName = "CGame/World/Game Bootstrap")]
    public sealed class GameBootstrap : WorldConfiguration
    {
        [SerializeField] private string resourcePackageName = "DefaultPackage";
        [SerializeField] private bool initializeResources = true;
        [SerializeField] private bool initializeInput = true;
        [SerializeField] private CharacterPhysicsSettings characterPhysicsSettings;
        [SerializeField] private GameModeDefinition gameModeDefinition;
        [SerializeField] private GameplayTagSource[] gameplayTagSources;
        [SerializeField] private WeaponDefinition[] weaponDefinitions;

        public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems()
        {
            var subSystems = new List<WorldSubSystem>
            {
                new CharacterPhysicsSubSystem(characterPhysicsSettings),
                new GameplayTagWorldCoreService(gameplayTagSources),
                new WeaponCatalogSubSystem(weaponDefinitions)
            };
            if (initializeResources)
            {
                subSystems.Add(new ResourceManager(resourcePackageName));
                subSystems.Add(new AssetManager());
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
            return gameModeDefinition == null
                ? null
                : gameModeDefinition.CreateGameMode(world, player);
        }

public void ConfigureGameplayTagSources(params GameplayTagSource[] sources)
        {
            gameplayTagSources = sources ?? System.Array.Empty<GameplayTagSource>();
        }

        public void ConfigureGameMode(GameModeDefinition definition)
        {
            gameModeDefinition = definition ?? throw new System.ArgumentNullException(nameof(definition));
        }

        public void ConfigureWeaponDefinitions(params WeaponDefinition[] definitions)
        {
            weaponDefinitions = definitions ?? System.Array.Empty<WeaponDefinition>();
        }


public void ConfigureResourceInitialization(bool enabled)
        {
            initializeResources = enabled;
        }
}
}
