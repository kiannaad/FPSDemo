using System;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedServerBootstrap : MonoBehaviour
    {
        [SerializeField] private WorldConfiguration gameBootstrap;
        public WorldConfiguration Configuration => gameBootstrap;

        public void Configure(WorldConfiguration bootstrap) =>
            gameBootstrap = bootstrap != null ? bootstrap : throw new ArgumentNullException(nameof(bootstrap));

        private async void Awake()
        {
            try
            {
                string[] arguments = Environment.GetCommandLineArgs();
                int firstDedicatedOption = Array.IndexOf(arguments, "--match-id");
                if (firstDedicatedOption < 0) return;
                string[] dedicatedArguments = new string[arguments.Length - firstDedicatedOption];
                Array.Copy(arguments, firstDedicatedOption, dedicatedArguments, 0, dedicatedArguments.Length);
                DedicatedServerLaunchConfiguration launch = DedicatedServerCommandLine.Parse(dedicatedArguments);
                DedicatedServerRuntime runtime = gameObject.AddComponent<DedicatedServerRuntime>();
                await runtime.StartAsync(launch, gameBootstrap);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Application.Quit(2);
            }
        }
    }
}
