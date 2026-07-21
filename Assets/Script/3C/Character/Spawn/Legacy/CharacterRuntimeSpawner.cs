using System;
using CGame.Animation;
using UnityEngine;

namespace CGame
{
    /// <summary>
    /// 集中装配本地玩家角色，并在任一步失败时回滚已创建资源。
    /// </summary>
    public sealed class CharacterRuntimeSpawner
    {
        private readonly PawnManager pawnManager;
        private readonly ControllerManager controllerManager;

        public CharacterRuntimeSpawner(PawnManager pawnManager, ControllerManager controllerManager)
        {
            this.pawnManager = pawnManager ?? throw new ArgumentNullException(nameof(pawnManager));
            this.controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));
        }

        public CharacterRuntime SpawnPlayer(in PlayerCharacterSpawnRequest request)
        {
            if (request.AnimationConfig == null || !request.AnimationConfig.IsValid)
            {
                throw new ArgumentException("A valid character animation config is required.", nameof(request));
            }

            if (request.VisualPrefab == null)
            {
                throw new ArgumentException("A character visual prefab is required.", nameof(request));
            }

            if (request.Input == null)
            {
                throw new ArgumentNullException(nameof(request), "Player input is required.");
            }

            CharacterAssembly assembly = null;
            PlayerController controller = null;
            bool pawnRegistered = false;

            try
            {
                assembly = new CharacterAssembler().Assemble(
                    request.VisualPrefab,
                    request.AnimationConfig,
                    request.Parent,
                    request.Position,
                    request.Rotation,
                    request.Name);

                pawnManager.RegisteringPawn(assembly.Character);
                pawnRegistered = true;
                controller = controllerManager.CreatingController<PlayerController>();
                controller.SettingInputHandle(request.Input);
                controller.PossessingPawn(assembly.Character);

                assembly.Root.SetActive(true);
                assembly.TransferRuntimeOwnership();
                return new CharacterRuntime(assembly.Root, assembly.Character, assembly.PawnHost, assembly.Motor, controller, pawnManager, controllerManager);
            }
            catch
            {
                controller?.SettingInputHandle(null);
                controllerManager.UnregisteringController(controller);
                if (pawnRegistered)
                {
                    pawnManager.UnregisteringPawn(assembly?.Character);
                }

                assembly?.Dispose();
                throw;
            }
        }
    }
}
