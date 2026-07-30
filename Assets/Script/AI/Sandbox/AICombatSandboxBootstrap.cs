using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame
{
    public sealed class AICombatSandboxBootstrap : MonoBehaviour
    {
        private const string CharacterDefinitionResource = "CharacterDefinition";
        private const string PlayerEntityId = "sandbox-player";
        private const int EnemyCount = 6;

        private static readonly Vector3 PlayerSpawnPosition = new Vector3(0f, 0f, -5f);
        private static readonly Vector3[] EnemySpawnPositions =
        {
            new Vector3(-4.5f, 0f, -3.5f),
            new Vector3(4.5f, 0f, -3.5f),
            new Vector3(-4.5f, 0f, 0f),
            new Vector3(4.5f, 0f, 0f),
            new Vector3(-4.5f, 0f, 3.5f),
            new Vector3(4.5f, 0f, 3.5f),
        };

        private readonly List<CharacterSpawnOperation> enemyOperations = new List<CharacterSpawnOperation>();
        private readonly HashSet<CharacterRuntimeId> configuredEnemies = new HashSet<CharacterRuntimeId>();
        private CharacterSpawnManager spawnManager;
        private CharacterSpawnOperation playerOperation;
        private AIPerceptionTargetBehaviour playerTarget;
        private HealthComponent playerHealth;
        private string failureMessage = string.Empty;
        private bool initialized;

        public bool PlayerReady => playerOperation != null
            && playerOperation.State == CharacterSpawnState.CharacterReady;
        public int RequestedAICount => enemyOperations.Count;
        public int ReadyAICount => configuredEnemies.Count;
        public bool IsReady => PlayerReady && ReadyAICount == EnemyCount && string.IsNullOrEmpty(failureMessage);
        public string FailureMessage => failureMessage;
        public HealthComponent PlayerHealth => playerHealth;

        private void Start()
        {
            Screen.SetResolution(1920, 1080, false);
            InitializeSandbox();
        }

        private void Update()
        {
            if (!initialized || !string.IsNullOrEmpty(failureMessage))
            {
                return;
            }

            DetectFailures();
            if (!string.IsNullOrEmpty(failureMessage))
            {
                return;
            }

            ConfigurePlayerTarget();
            ConfigureReadyEnemies();
        }

        private void OnDestroy()
        {
            ReleaseOperations();
        }

        private void InitializeSandbox()
        {
            CharacterDefinition definition = Resources.Load<CharacterDefinition>(CharacterDefinitionResource);
            if (definition == null)
            {
                failureMessage = $"Missing Resources/{CharacterDefinitionResource}.";
                Debug.LogError($"[AICombatSandbox] {failureMessage}", this);
                return;
            }

            try
            {
                _ = GameManager.Instance;
                spawnManager = GameManager.GetManager<CharacterSpawnManager>();

                HidePresentationRoot("PerceptionDebug");
                HidePresentationRoot("DecisionDebug");

                playerOperation = spawnManager.BeginSpawn(CreateRequest(
                    definition.DefinitionId,
                    CharacterControlKind.LocalPlayer,
                    "SandboxPlayer",
                    PlayerSpawnPosition,
                    Quaternion.identity));

                for (int i = 0; i < EnemySpawnPositions.Length; i++)
                {
                    Vector3 position = EnemySpawnPositions[i];
                    Quaternion rotation = Quaternion.LookRotation(PlayerSpawnPosition - position, Vector3.up);
                    enemyOperations.Add(spawnManager.BeginSpawn(CreateRequest(
                        definition.DefinitionId,
                        CharacterControlKind.AI,
                        $"SandboxAI-{i + 1}",
                        position,
                        rotation)));
                }

                initialized = true;
            }
            catch (Exception exception)
            {
                failureMessage = exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void ConfigurePlayerTarget()
        {
            if (playerTarget != null || !PlayerReady)
            {
                return;
            }

            if (!spawnManager.TryGetCharacterView(playerOperation.RuntimeId, out ICharacterView playerView))
            {
                return;
            }

            Transform playerTransform = playerView.Transform;
            playerHealth = playerTransform.GetComponent<HealthComponent>();
            if (playerHealth == null)
            {
                playerHealth = playerTransform.gameObject.AddComponent<HealthComponent>();
            }

            playerHealth.Configure(PlayerEntityId, 500f);
            playerTarget = playerTransform.GetComponent<AIPerceptionTargetBehaviour>();
            if (playerTarget == null)
            {
                playerTarget = playerTransform.gameObject.AddComponent<AIPerceptionTargetBehaviour>();
            }

            playerTarget.Configure(PlayerEntityId, playerTransform);
        }

        private void ConfigureReadyEnemies()
        {
            if (playerTarget == null)
            {
                return;
            }

            for (int i = 0; i < enemyOperations.Count; i++)
            {
                CharacterSpawnOperation operation = enemyOperations[i];
                if (operation.State != CharacterSpawnState.CharacterReady
                    || configuredEnemies.Contains(operation.RuntimeId)
                    || !spawnManager.TryGetAIRuntime(operation.RuntimeId, out AIRuntimeRegistration runtime))
                {
                    continue;
                }

                runtime.Perception.RegisterTarget(playerTarget);
                runtime.DebugRuntime?.SetPanelVisible(false);
                configuredEnemies.Add(operation.RuntimeId);
            }
        }

        private void DetectFailures()
        {
            if (playerOperation != null && playerOperation.State == CharacterSpawnState.Failed)
            {
                RecordFailure("player", playerOperation.Error);
                return;
            }

            for (int i = 0; i < enemyOperations.Count; i++)
            {
                if (enemyOperations[i].State == CharacterSpawnState.Failed)
                {
                    RecordFailure($"AI {i + 1}", enemyOperations[i].Error);
                    return;
                }
            }
        }

        private void RecordFailure(string actor, CharacterSpawnError error)
        {
            failureMessage = $"Failed to spawn {actor}: {error}.";
            Debug.LogError($"[AICombatSandbox] {failureMessage}", this);
        }

        private void ReleaseOperations()
        {
            if (spawnManager == null)
            {
                return;
            }

            ReleaseOperation(playerOperation);
            for (int i = 0; i < enemyOperations.Count; i++)
            {
                ReleaseOperation(enemyOperations[i]);
            }

            enemyOperations.Clear();
            configuredEnemies.Clear();
            playerOperation = null;
            playerTarget = null;
            playerHealth = null;
            spawnManager = null;
        }

        private void ReleaseOperation(CharacterSpawnOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            if (operation.State == CharacterSpawnState.CharacterReady)
            {
                spawnManager.Despawn(operation.RuntimeId, CharacterDespawnReason.Requested);
            }
            else if (!operation.IsComplete || operation.State == CharacterSpawnState.CancelRequested)
            {
                spawnManager.CancelSpawn(operation.Request.RequestId);
            }
        }

        private static CharacterSpawnRequest CreateRequest(
            CharacterDefinitionId definitionId,
            CharacterControlKind controlKind,
            string displayName,
            Vector3 position,
            Quaternion rotation)
        {
            return new CharacterSpawnRequest(
                new CharacterSpawnRequestId(Guid.NewGuid().ToString("N")),
                definitionId,
                controlKind,
                new CharacterSpawnPlacement(position, rotation),
                InputType.Player,
                displayName);
        }

        private static void HidePresentationRoot(string rootName)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, rootName, StringComparison.Ordinal))
                {
                    roots[i].SetActive(false);
                    return;
                }
            }
        }
    }
}
