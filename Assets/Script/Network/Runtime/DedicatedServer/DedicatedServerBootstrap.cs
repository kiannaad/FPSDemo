using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Network
{
    public sealed class DedicatedServerBootstrap : MonoBehaviour
    {
        [SerializeField] private WorldConfiguration gameBootstrap;
        public WorldConfiguration Configuration => gameBootstrap;

        public void Configure(WorldConfiguration bootstrap) =>
            gameBootstrap = bootstrap != null ? bootstrap : throw new ArgumentNullException(nameof(bootstrap));

        public WorldConfiguration ResolveLevelConfiguration(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Dedicated level must be loaded before resolving its configuration.");
            GameInstance levelInstance = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (GameInstance candidate in root.GetComponentsInChildren<GameInstance>(true))
            {
                if (levelInstance != null)
                    throw new InvalidOperationException($"Level {scene.name} has multiple GameInstance configurations.");
                levelInstance = candidate;
            }
            return levelInstance != null
                ? levelInstance.Configuration ?? throw new InvalidOperationException($"Level {scene.name} has no configuration.")
                : gameBootstrap;
        }

        private void Awake()
        {
            try
            {
                string[] arguments = Environment.GetCommandLineArgs();
                int firstDedicatedOption = Array.IndexOf(arguments, "--match-id");
                if (firstDedicatedOption < 0) return;
                DontDestroyOnLoad(gameObject);
                Application.runInBackground = true;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                string[] dedicatedArguments = new string[arguments.Length - firstDedicatedOption];
                Array.Copy(arguments, firstDedicatedOption, dedicatedArguments, 0, dedicatedArguments.Length);
                DedicatedServerLaunchConfiguration launch = DedicatedServerCommandLine.Parse(dedicatedArguments);
                StartCoroutine(StartDedicatedServer(launch));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Application.Quit(2);
            }
        }

        private IEnumerator StartDedicatedServer(DedicatedServerLaunchConfiguration launch)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(launch.LevelId, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                Debug.LogError($"Unable to load Dedicated Server level {launch.LevelId}.");
                Application.Quit(2);
                yield break;
            }

            yield return loadOperation;
            DedicatedServerRuntime runtime = gameObject.AddComponent<DedicatedServerRuntime>();
            WorldConfiguration levelConfiguration = ResolveLevelConfiguration(SceneManager.GetSceneByName(launch.LevelId));
            Task startTask = runtime.StartAsync(launch, levelConfiguration, loadLevel: false);
            yield return new WaitUntil(() => startTask.IsCompleted);
            if (startTask.IsFaulted)
            {
                Debug.LogException(startTask.Exception);
                Application.Quit(2);
            }
        }
    }
}
