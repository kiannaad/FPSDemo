using System;
using System.Collections.Generic;
using System.Linq;
using CGame.Ability;
using CGame.Ability.Animation;
using CGame.Animation;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Weapon.Equipment.Tests
{
    public sealed class WeaponActionAbilityTests
    {
        private readonly List<UnityEngine.Object> objectsToDestroy =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            foreach (UnityEngine.Object target in objectsToDestroy)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }

            objectsToDestroy.Clear();
        }

        [Test]
        public void Fire_CommitsOnlyAtMatchingEvent_AndDuplicateEventDoesNotCommitTwice()
        {
            InitializeTags();
            var fixture = CreateFixture(WeaponActionKind.Fire);
            var commits = new List<WeaponActionFact>();
            fixture.Runtime.FireCommitted += commits.Add;

            AbilityActivationResult activation = fixture.AbilitySystem
                .TryActivateAbilityByTag(WeaponActionGameplayTags.FireAbility);

            Assert.That(activation.Succeeded, Is.True);
            Assert.That(fixture.Runtime.ActiveAction.Kind, Is.EqualTo(WeaponActionKind.Fire));
            Assert.That(commits, Is.Empty, "Begin must not commit the fire result.");

            Dispatch(fixture, WeaponActionGameplayTags.FireEvent, activation.ActivationHandle);
            Dispatch(fixture, WeaponActionGameplayTags.FireEvent, activation.ActivationHandle);

            Assert.That(commits.Count, Is.EqualTo(1));
            Assert.That(commits[0].ActionId, Is.EqualTo(fixture.Runtime.ActiveAction.ActionId));
            fixture.Player.Complete();
            Assert.That(fixture.Runtime.ActiveAction.IsValid, Is.False);
            Assert.That(fixture.Instance.LastEndReason, Is.EqualTo(AbilityEndReason.Completed));
        }

        [Test]
        public void Reload_NaturalCompletionWithoutNotify_CancelsActionAndFailsAbility()
        {
            InitializeTags();
            var fixture = CreateFixture(WeaponActionKind.Reload);

            Assert.That(
                fixture.AbilitySystem.TryActivateAbilityByTag(
                    WeaponActionGameplayTags.ReloadAbility).Succeeded,
                Is.True);

            fixture.Player.Complete();

            Assert.That(fixture.Runtime.ActiveAction.IsValid, Is.False);
            Assert.That(fixture.LastAction.Phase, Is.EqualTo(WeaponActionPhase.Cancelled));
            Assert.That(fixture.LastAction.EndReason, Is.EqualTo(WeaponActionEndReason.AnimationFailed));
            Assert.That(fixture.Instance.LastEndReason, Is.EqualTo(AbilityEndReason.Failed));
        }

        [Test]
        public void ActionAbilities_AreMutuallyExclusiveAcrossFireAndReload()
        {
            InitializeTags();
            var fixture = CreateFixture(WeaponActionKind.Reload);
            Assert.That(fixture.AbilitySystem.TryActivateAbilityByTag(
                WeaponActionGameplayTags.FireAbility).Succeeded, Is.True);

            AbilityActivationResult reload = fixture.AbilitySystem.TryActivateAbilityByTag(
                WeaponActionGameplayTags.ReloadAbility);

            Assert.That(reload.Succeeded, Is.False);
            Assert.That(reload.FailureTags, Does.Contain(AbilityFailureTags.BlockedTagPresent));
            Assert.That(fixture.Runtime.ActiveAction.Kind, Is.EqualTo(WeaponActionKind.Fire));
        }

        [Test]
        public void WrongSourceEvent_IsIgnoredBeforeMatchingEventCommits()
        {
            InitializeTags();
            var fixture = CreateFixture(WeaponActionKind.Fire);
            AbilityActivationResult activation = fixture.AbilitySystem.TryActivateAbilityByTag(
                WeaponActionGameplayTags.FireAbility);

            fixture.AbilitySystem.HandleGameEvent(
                WeaponActionGameplayTags.FireEvent,
                new AbilityGameEventPayload(
                    fixture.AbilitySystem,
                    fixture.Pawn,
                    new object(),
                    activation.ActivationHandle));
            Assert.That(fixture.Instance.HasCommitted, Is.False);

            Dispatch(fixture, WeaponActionGameplayTags.FireEvent, activation.ActivationHandle);
            Assert.That(fixture.Instance.HasCommitted, Is.True);
        }

        [Test]
        public void ImmediatePlaybackFailure_CancelsRuntimeAndFailsAbility()
        {
            InitializeTags();
            var fixture = CreateFixture(WeaponActionKind.Fire);
            fixture.Player.FailOnPlay = true;

            AbilityActivationResult activation = fixture.AbilitySystem.TryActivateAbilityByTag(
                WeaponActionGameplayTags.FireAbility);

            Assert.That(activation.Succeeded, Is.True);
            Assert.That(fixture.Runtime.ActiveAction.IsValid, Is.False);
            Assert.That(fixture.LastAction.EndReason, Is.EqualTo(WeaponActionEndReason.AnimationFailed));
            Assert.That(fixture.Instance.LastEndReason, Is.EqualTo(AbilityEndReason.Failed));
        }

        [Test]
        public void SourceRemoved_StopsPlayback_CancelsRuntime_AndOldEventIsIgnored()
        {
            InitializeTags();
            var fixture = CreateFixture(WeaponActionKind.MeleeAttack);
            AbilityActivationResult activation = fixture.AbilitySystem
                .TryActivateAbilityByTag(WeaponActionGameplayTags.MeleeAbility);
            Assert.That(activation.Succeeded, Is.True);

            fixture.Equipment.Dispose();
            Dispatch(fixture, WeaponActionGameplayTags.MeleeEvent, activation.ActivationHandle);

            Assert.That(fixture.Player.StopCount, Is.EqualTo(1));
            Assert.That(fixture.Runtime.ActiveAction.IsValid, Is.False);
            Assert.That(fixture.LastAction.EndReason, Is.EqualTo(WeaponActionEndReason.Unequipped));
            Assert.That(fixture.Instance.LastEndReason, Is.EqualTo(AbilityEndReason.SourceRemoved));
        }

        [Test]
        public void Controller_RoutesPrimaryAndReloadThroughGrantedExactAbilityTags()
        {
            InitializeTags();
            var fixture = CreateFixture(WeaponActionKind.Reload);
            Assert.That(fixture.Controller.RequestPrimaryWeaponAction(out WeaponActionFact fire), Is.True);
            Assert.That(fire.Kind, Is.EqualTo(WeaponActionKind.Fire));
            Dispatch(fixture, WeaponActionGameplayTags.FireEvent, default);
            fixture.Player.Complete();
            Assert.That(fixture.Controller.RequestReloadWeapon(out WeaponActionFact reload), Is.True);
            Assert.That(reload.Kind, Is.EqualTo(WeaponActionKind.Reload));
        }

        [TestCase("Assets/Art/Animation/Weapon/KINEMATION/AK/RifleAKAnimationDefinition.asset",
            WeaponActionKind.Fire, "Event.Weapon.Fire", 2)]
        [TestCase("Assets/Art/Animation/Weapon/KINEMATION/AK/RifleAKAnimationDefinition.asset",
            WeaponActionKind.Reload, "Event.Weapon.Reload", 91)]
        [TestCase("Assets/Resources/KnifeWeaponAnimationDefinition.asset",
            WeaponActionKind.MeleeAttack, "Event.Weapon.Melee", 28)]
        public void LiveActionAssets_HaveOneMatchingGameplayEventNotify(
            string definitionPath,
            WeaponActionKind kind,
            string eventTagPath,
            int expectedFrame)
        {
            GameplayTagSource source = AssetDatabase.LoadAssetAtPath<GameplayTagSource>(
                "Assets/Data/GamePlayTag/Sources/DefaultGameplayTags.asset");
            GameplayTagRegistryBuildResult build = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(build.Succeeded, Is.True);
            WeaponAnimationDefinition definition =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(definitionPath);
            AnimationClipAsset asset = kind == WeaponActionKind.Fire ? definition.Fire
                : kind == WeaponActionKind.Reload ? definition.Reload
                : definition.MeleeAttack;
            GameplayTag expectedTag = GameplayTagManager.Instance.RequestTag(eventTagPath);

            AnimationNotifyEvent[] matches = asset.NotifyTracks
                .SelectMany(track => track.Events)
                .Where(item => item.Notify is AnimationGameEventNotify notify
                    && notify.EventTag == expectedTag)
                .ToArray();

            Assert.That(matches.Length, Is.EqualTo(1));
            Assert.That(matches[0].StartFrame, Is.EqualTo(expectedFrame));
        }

        [Test]
        public void LiveRiflePresentation_EveryConfiguredModelClipBindsToItsAnimatorHierarchy()
        {
            WeaponAnimationDefinition definition =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                    "Assets/Art/Animation/Weapon/KINEMATION/AK/RifleAKAnimationDefinition.asset");
            WeaponPresentationInstance presentation =
                definition.WeaponPrefab.GetComponent<WeaponPresentationInstance>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.ModelActionPlayer, Is.Not.Null);
            AnimationClipAsset[] modelAssets =
            {
                definition.WeaponModelFire,
                definition.WeaponModelReload
            };
            Assert.That(modelAssets.Count(asset => asset != null), Is.GreaterThan(0));

            var serializedPlayer = new SerializedObject(presentation.ModelActionPlayer);
            Animator targetAnimator = serializedPlayer.FindProperty("animator")
                .objectReferenceValue as Animator;
            Assert.That(targetAnimator, Is.Not.Null);
            foreach (AnimationClipAsset modelAsset in modelAssets.Where(asset => asset != null))
            {
                string[] paths = AnimationUtility.GetCurveBindings(modelAsset.AnimationClip)
                    .Select(binding => binding.path)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Distinct()
                    .ToArray();

                Assert.That(paths, Is.Not.Empty);
                Assert.That(
                    paths.All(path => targetAnimator.transform.Find(path) != null),
                    Is.True,
                    $"Every curve in '{modelAsset.name}' must resolve below the presentation Animator.");
            }
        }

        [Test]
        public void PresentationController_AttachesToRightHand_AndRoutesModelFireLifecycle()
        {
            var characterRoot = new GameObject("Character");
            objectsToDestroy.Add(characterRoot);
            Animator characterAnimator = characterRoot.AddComponent<Animator>();
            var rightHand = new GameObject("Right_Hand");
            rightHand.transform.SetParent(characterRoot.transform, false);

            var template = new GameObject("RiflePresentation");
            objectsToDestroy.Add(template);
            WeaponPresentationInstance presentation =
                template.AddComponent<WeaponPresentationInstance>();
            WeaponModelActionPlayer modelPlayer =
                template.AddComponent<WeaponModelActionPlayer>();
            var model = new GameObject("Model");
            model.transform.SetParent(template.transform, false);
            Animator modelAnimator = model.AddComponent<Animator>();
            var modelRoot = new GameObject("Root");
            modelRoot.transform.SetParent(model.transform, false);
            var mount = new GameObject("RightHandMount");
            mount.transform.SetParent(template.transform, false);
            var grip = new GameObject("LeftHandGrip");
            grip.transform.SetParent(template.transform, false);
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(template.transform, false);
            var serializedPresentation = new SerializedObject(presentation);
            serializedPresentation.FindProperty("rightHandMount").objectReferenceValue =
                mount.transform;
            serializedPresentation.FindProperty("leftHandGrip").objectReferenceValue =
                grip.transform;
            serializedPresentation.FindProperty("muzzle").objectReferenceValue =
                muzzle.transform;
            serializedPresentation.FindProperty("modelActionPlayer").objectReferenceValue =
                modelPlayer;
            serializedPresentation.ApplyModifiedPropertiesWithoutUndo();
            var serializedPlayer = new SerializedObject(modelPlayer);
            serializedPlayer.FindProperty("animator").objectReferenceValue = modelAnimator;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            var bodyClip = new AnimationClip();
            bodyClip.SetCurve("", typeof(Transform), "m_LocalScale.x",
                AnimationCurve.Linear(0f, 1f, 0.2f, 1f));
            objectsToDestroy.Add(bodyClip);
            var bodyAsset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.That(bodyAsset.TryInitialize(bodyClip), Is.True);
            objectsToDestroy.Add(bodyAsset);
            var modelClip = new AnimationClip();
            modelClip.SetCurve("Root", typeof(Transform), "m_LocalScale.x",
                AnimationCurve.Linear(0f, 1f, 0.1f, 1.1f));
            objectsToDestroy.Add(modelClip);
            var modelAsset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.That(modelAsset.TryInitialize(modelClip), Is.True);
            objectsToDestroy.Add(modelAsset);
            var definition = ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
            objectsToDestroy.Add(definition);
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("weaponId").stringValue = "rifle";
            serializedDefinition.FindProperty("weaponPrefab").objectReferenceValue = template;
            serializedDefinition.FindProperty("supportsFire").boolValue = true;
            serializedDefinition.FindProperty("fire").objectReferenceValue = bodyAsset;
            serializedDefinition.FindProperty("weaponModelFire").objectReferenceValue = modelAsset;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            var runtime = new WeaponRuntime();
            Assert.That(runtime.Initialize(new WeaponId("rifle"),
                new WeaponRuntimeCapabilities(true, false, false)), Is.True);
            using (var controller = new CharacterWeaponPresentationController(characterAnimator))
            {
                controller.BindRuntime(runtime);
                Assert.That(controller.TryEquip(definition, 1u), Is.True);
                Assert.That(controller.CurrentPresentation.transform.parent,
                    Is.SameAs(rightHand.transform));
                Assert.That(runtime.RequestFire(out WeaponActionFact action), Is.True);
                Assert.That(controller.CurrentPresentation.ModelActionPlayer.ActionId,
                    Is.EqualTo(action.ActionId));
                Vector3 basePosition = controller.CurrentPresentation.transform.localPosition;
                controller.Update(0.02f);
                Assert.That(controller.CurrentPresentation.transform.localPosition,
                    Is.Not.EqualTo(basePosition));

                Assert.That(runtime.CancelAction(action.ActionId), Is.True);
                Assert.That(controller.CurrentPresentation.ModelActionPlayer.IsPlaying, Is.False);
                Assert.That(controller.CurrentPresentation.transform.localPosition,
                    Is.EqualTo(basePosition));
            }
        }

        [TestCase("Assets/Art/Animation/Weapon/KINEMATION/AK/RifleAKAnimationDefinition.asset",
            WeaponActionKind.Fire)]
        [TestCase("Assets/Art/Animation/Weapon/KINEMATION/AK/RifleAKAnimationDefinition.asset",
            WeaponActionKind.Reload)]
        [TestCase("Assets/Resources/KnifeWeaponAnimationDefinition.asset",
            WeaponActionKind.MeleeAttack)]
        public void LiveFirstPersonSkeletonActions_MaskOutTorsoNeckAndHead(
            string definitionPath,
            WeaponActionKind kind)
        {
            WeaponAnimationDefinition definition =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(definitionPath);
            AnimationClipAsset asset = kind == WeaponActionKind.Fire
                ? definition.Fire
                : kind == WeaponActionKind.Reload
                    ? definition.Reload
                    : definition.MeleeAttack;
            AvatarMask mask = asset.Mask;
            Assert.That(mask, Is.Not.Null);
            var activePaths = new HashSet<string>(
                Enumerable.Range(0, mask.transformCount)
                    .Where(mask.GetTransformActive)
                    .Select(mask.GetTransformPath));

            Assert.That(activePaths, Does.Contain(
                "Skeleton/Hips/Spine/Chest/UpperChest/Left_Shoulder/Left_UpperArm"));
            Assert.That(activePaths, Does.Contain(
                "Skeleton/Hips/Spine/Chest/UpperChest/Right_Shoulder/Right_UpperArm"));
            Assert.That(activePaths, Does.Not.Contain("Skeleton/Hips/Spine"));
            Assert.That(activePaths, Does.Not.Contain("Skeleton/Hips/Spine/Chest"));
            Assert.That(activePaths, Does.Not.Contain("Skeleton/Hips/Spine/Chest/UpperChest"));
            Assert.That(activePaths.Any(path => path.EndsWith("/Head", StringComparison.Ordinal)),
                Is.False);
        }

        private Fixture CreateFixture(WeaponActionKind kind)
        {
            string weaponId = kind == WeaponActionKind.MeleeAttack ? "knife" : "rifle";
            var definition = CreateDefinition(weaponId, kind);
            var lease = new FakeLease(definition);
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            var runtime = controller.WeaponRuntime;
            Assert.That(controller.InitializeWeapon(new WeaponId(weaponId), definition.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var player = new FakeAnimationPlayer();
            var slot = new EquipmentSlot(abilitySystem, runtime, player);
            AbilitySet set = WeaponActionAbilitySetFactory.Create(definition);
            Assert.That(slot.TryEquip(lease, new[] { set }, out EquipmentInstance equipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            AbilitySpec spec = equipment.GrantReceipts[0].SpecHandles
                .Select(handle => abilitySystem.TryGetSpec(handle, out AbilitySpec candidate)
                    ? candidate
                    : null)
                .Single(candidate =>
                    candidate?.Definition is WeaponActionAbilityDefinition actionDefinition
                    && actionDefinition.ActionKind == kind);
            var fixture = new Fixture(abilitySystem, pawn, controller, runtime, equipment, player,
                (WeaponActionAbilityInstance)spec.PrimaryInstance);
            runtime.ActionChanged += fact =>
            {
                if (fact.Phase != WeaponActionPhase.Started)
                {
                    fixture.LastAction = fact;
                }
            };
            return fixture;
        }

        private void Dispatch(Fixture fixture, GameplayTag tag, AbilityActivationHandle activationHandle)
        {
            fixture.AbilitySystem.HandleGameEvent(tag, new AbilityGameEventPayload(
                fixture.AbilitySystem,
                fixture.Pawn,
                fixture.Equipment,
                activationHandle));
        }

        private void InitializeTags()
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("WeaponActionTests", new[]
            {
                Node("Ability", false, Node("Weapon", false,
                    Node("Fire", true), Node("Reload", true), Node("Melee", true))),
                Node("Event", false, Node("Weapon", false,
                    Node("Fire", true), Node("Reload", true), Node("Melee", true))),
                Node("State", false, Node("Weapon", false, Node("Action", true)))
            });
            objectsToDestroy.Add(source);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True,
                string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        }

        private WeaponAnimationDefinition CreateDefinition(string weaponId, WeaponActionKind kind)
        {
            var clip = new AnimationClip();
            objectsToDestroy.Add(clip);
            var clipAsset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.That(clipAsset.TryInitialize(clip), Is.True);
            objectsToDestroy.Add(clipAsset);
            var definition = ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
            objectsToDestroy.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("weaponId").stringValue = weaponId;
            if (kind == WeaponActionKind.Fire)
            {
                serialized.FindProperty("supportsFire").boolValue = true;
                serialized.FindProperty("fire").objectReferenceValue = clipAsset;
            }
            else if (kind == WeaponActionKind.Reload)
            {
                serialized.FindProperty("supportsFire").boolValue = true;
                serialized.FindProperty("supportsReload").boolValue = true;
                serialized.FindProperty("fire").objectReferenceValue = clipAsset;
                serialized.FindProperty("reload").objectReferenceValue = clipAsset;
            }
            else
            {
                serialized.FindProperty("supportsMeleeAttack").boolValue = true;
                serialized.FindProperty("meleeAttack").objectReferenceValue = clipAsset;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static GameplayTagSourceNode Node(string segment, bool explicitTag,
            params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segment, explicitTag, children: children);
        }

        private sealed class FakeAnimationPlayer : IAbilityAnimationPlayer
        {
            private readonly FakePlayback playback = new FakePlayback();
            public event Action Updated;
            public int StopCount { get; private set; }
            public bool FailOnPlay { get; set; }

            public IAbilityAnimationPlayback PlayAnimation(AnimationClipAsset asset, long requestId)
            {
                playback.State = FailOnPlay
                    ? AbilityAnimationPlaybackState.Failed
                    : AbilityAnimationPlaybackState.Playing;
                return playback;
            }

            public bool StopAnimation(IAbilityAnimationPlayback target)
            {
                StopCount++;
                playback.State = AbilityAnimationPlaybackState.Cancelled;
                return true;
            }

            public void Complete()
            {
                playback.State = AbilityAnimationPlaybackState.Completed;
                Updated?.Invoke();
            }
        }

        private sealed class FakePlayback : IAbilityAnimationPlayback
        {
            public AbilityAnimationPlaybackState State { get; set; }
            public bool IsTerminal => State == AbilityAnimationPlaybackState.Completed
                || State == AbilityAnimationPlaybackState.Cancelled
                || State == AbilityAnimationPlaybackState.Interrupted
                || State == AbilityAnimationPlaybackState.Failed;
        }

        private sealed class FakeLease : IEquipmentDefinitionLease
        {
            public FakeLease(WeaponAnimationDefinition definition) { Definition = definition; }
            public WeaponAnimationDefinition Definition { get; }
            public WeaponId WeaponId => Definition.WeaponId;
            public bool IsValid => !IsDisposed;
            public bool IsDisposed { get; private set; }
            public void Dispose() { IsDisposed = true; }
        }

        private sealed class Fixture
        {
            public Fixture(AbilitySystemComponent abilitySystem, Pawn pawn, Controller controller,
                WeaponRuntime runtime,
                EquipmentInstance equipment, FakeAnimationPlayer player,
                WeaponActionAbilityInstance instance)
            {
                AbilitySystem = abilitySystem;
                Pawn = pawn;
                Controller = controller;
                Runtime = runtime;
                Equipment = equipment;
                Player = player;
                Instance = instance;
            }

            public AbilitySystemComponent AbilitySystem { get; }
            public Pawn Pawn { get; }
            public Controller Controller { get; }
            public WeaponRuntime Runtime { get; }
            public EquipmentInstance Equipment { get; }
            public FakeAnimationPlayer Player { get; }
            public WeaponActionAbilityInstance Instance { get; }
            public WeaponActionFact LastAction { get; set; }
        }
    }
}
