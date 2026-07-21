using System;
using UnityEngine;

namespace CGame
{
    public sealed class AIControllerBinder
    {
        private readonly ControllerManager controllerManager;
        private readonly AIRuntimeRegistry runtimeRegistry;
        private readonly AIPrototypeLoadout loadout;

        public AIControllerBinder(
            ControllerManager controllerManager,
            AIRuntimeRegistry runtimeRegistry,
            AIPrototypeLoadout loadout)
        {
            this.controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));
            this.runtimeRegistry = runtimeRegistry ?? throw new ArgumentNullException(nameof(runtimeRegistry));
            this.loadout = loadout;
        }

        public AIControllerBinding Bind(CharacterRuntimeId runtimeId, CharacterAssembly assembly)
        {
            if (!runtimeId.IsValid)
            {
                throw new ArgumentException("A valid character runtime ID is required.", nameof(runtimeId));
            }

            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            if (loadout == null || !loadout.IsValid)
            {
                throw new InvalidOperationException("A valid AI prototype loadout is required.");
            }

            Transform rightHand = assembly.Animator.isHuman
                ? assembly.Animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;
            if (rightHand == null)
            {
                throw new InvalidOperationException("The AI character requires a humanoid right-hand bone.");
            }

            GameObject rifle = null;
            HealthComponent health = null;
            WeaponRuntimeBehaviour weaponRuntime = null;
            AIController controller = null;
            IControllerRegistration controllerRegistration = null;
            AIRuntimeRegistration runtimeRegistration = null;
            AINavigationRuntimeBehaviour navigationRuntime = null;
            AIPerceptionRuntimeBehaviour perceptionRuntime = null;
            AISquadMemberRuntimeBehaviour squadMemberRuntime = null;
            AIAlertDecisionRuntimeBehaviour decisionRuntime = null;
            AICoverCombatRuntimeBehaviour coverCombatRuntime = null;
            AICombatDebugRuntimeBehaviour debugRuntime = null;
            try
            {
                rifle = UnityEngine.Object.Instantiate(loadout.RiflePrefab, rightHand);
                rifle.name = "PrototypeRifle";
                rifle.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrototypeRifleView rifleView = rifle.GetComponent<PrototypeRifleView>();
                if (rifleView == null || !rifleView.IsValid)
                {
                    throw new InvalidOperationException("The prototype rifle prefab is missing its configured view markers.");
                }

                health = assembly.Root.AddComponent<HealthComponent>();
                health.Configure(runtimeId.Value, loadout.MaxHealth);
                weaponRuntime = assembly.Root.AddComponent<WeaponRuntimeBehaviour>();
                weaponRuntime.Initialize(
                    loadout.WeaponProfile,
                    rifleView.Muzzle,
                    rifle.transform,
                    runtimeId.Value,
                    runtimeId.Value.GetHashCode());

                controller = controllerManager.CreateController<AIController>(out controllerRegistration);
                controller.SettingCombatIntentSink(weaponRuntime);
                controller.PossessingPawn(assembly.Character);

                runtimeRegistration = new AIRuntimeRegistration(
                    runtimeId,
                    controller,
                    assembly.Root.transform,
                    health,
                    weaponRuntime,
                    rifleView.Muzzle,
                    rifleView.LeftHandSupport,
                    rifleView.RightHandGrip);
                navigationRuntime = assembly.Root.AddComponent<AINavigationRuntimeBehaviour>();
                navigationRuntime.Initialize(runtimeRegistration);
                runtimeRegistration.AttachNavigation(navigationRuntime);
                perceptionRuntime = assembly.Root.AddComponent<AIPerceptionRuntimeBehaviour>();
                perceptionRuntime.Initialize(runtimeRegistration, loadout.PerceptionProfile);
                runtimeRegistration.AttachPerception(perceptionRuntime);
                squadMemberRuntime = assembly.Root.AddComponent<AISquadMemberRuntimeBehaviour>();
                squadMemberRuntime.Initialize(runtimeRegistration, runtimeRegistry.SquadContext);
                runtimeRegistration.AttachSquadMember(squadMemberRuntime);
                decisionRuntime = assembly.Root.AddComponent<AIAlertDecisionRuntimeBehaviour>();
                decisionRuntime.Initialize(
                    runtimeRegistration,
                    loadout.DecisionProfile,
                    runtimeId.Value.GetHashCode());
                runtimeRegistration.AttachDecision(decisionRuntime);
                coverCombatRuntime = assembly.Root.AddComponent<AICoverCombatRuntimeBehaviour>();
                coverCombatRuntime.Initialize(runtimeRegistration, loadout.CombatProfile);
                runtimeRegistration.AttachCoverCombat(coverCombatRuntime);
                debugRuntime = assembly.Root.AddComponent<AICombatDebugRuntimeBehaviour>();
                debugRuntime.Initialize(runtimeRegistration);
                runtimeRegistration.AttachDebugRuntime(debugRuntime);
                runtimeRegistry.Add(runtimeRegistration);
                return new AIControllerBinding(
                    runtimeRegistry,
                    runtimeRegistration,
                    controller,
                    controllerRegistration,
                    health,
                    weaponRuntime);
            }
            catch
            {
                if (runtimeRegistration != null)
                {
                    runtimeRegistry.Remove(runtimeId, runtimeRegistration);
                }

                controller?.SettingCombatIntentSink(null);
                controller?.UnpossessingPawn();
                controllerRegistration?.Dispose();
                weaponRuntime?.Shutdown();
                navigationRuntime?.Shutdown();
                perceptionRuntime?.Shutdown();
                squadMemberRuntime?.Shutdown();
                decisionRuntime?.Shutdown();
                coverCombatRuntime?.Shutdown();
                debugRuntime?.Shutdown();
                DestroyObject(debugRuntime);
                DestroyObject(coverCombatRuntime);
                DestroyObject(decisionRuntime);
                DestroyObject(perceptionRuntime);
                DestroyObject(squadMemberRuntime);
                DestroyObject(navigationRuntime);
                DestroyObject(weaponRuntime);
                DestroyObject(health);
                DestroyObject(rifle);
                throw;
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
