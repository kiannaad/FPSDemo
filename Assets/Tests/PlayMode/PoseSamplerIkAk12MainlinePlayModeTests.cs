using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using CGame.Animation.Rig;
using CGame.InventoryEquipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.Animation.Tests
{
    public sealed class PoseSamplerIkAk12MainlinePlayModeTests
    {
        private const string AcceptanceScene = "Assets/Scenes/WeaponGripAK12AcceptanceScene.unity";

        [UnityTest]
        public IEnumerator AcceptanceScene_RunsIsolatedPoseSamplerAttachHandViewLookTurnIkProfile()
        {
            yield return SceneManager.LoadSceneAsync(AcceptanceScene, LoadSceneMode.Single);

            GameInstance gameInstance = UnityEngine.Object.FindObjectOfType<GameInstance>();
            Assert.That(gameInstance, Is.Not.Null, "Acceptance scene must contain the production GameInstance.");

            for (int frame = 0; frame < 300
                 && (gameInstance.InitializationTask == null
                     || !gameInstance.InitializationTask.IsCompleted
                     || gameInstance.RuntimeWorld?.State != WorldState.Playing); frame++)
            {
                yield return null;
            }

            Assert.That(gameInstance.InitializationTask, Is.Not.Null);
            Assert.That(gameInstance.InitializationTask.IsFaulted, Is.False,
                gameInstance.InitializationTask.Exception?.GetBaseException().Message);
            Assert.That(gameInstance.RuntimeWorld?.State, Is.EqualTo(WorldState.Playing),
                gameInstance.RuntimeWorld?.Failure);
            PlayerController controller = gameInstance.RuntimeWorld.GameMode.PlayerController as PlayerController;
            Assert.That(controller, Is.Not.Null, "Runtime GameMode must create a PlayerController.");
            Pawn pawn = controller.ControlledPawn;
            Assert.That(pawn, Is.Not.Null, "PlayerController must possess the acceptance Pawn.");
            EquipmentManagerComponent equipment = pawn.GetComponent<EquipmentManagerComponent>();
            PawnAnimationComponent animation = pawn.GetComponent<PawnAnimationComponent>();
            Assert.That(equipment, Is.Not.Null, "Acceptance Pawn must have EquipmentManagerComponent.");
            Assert.That(animation?.AnimInstance, Is.Not.Null, "Acceptance Pawn must initialize PawnAnimationComponent.");

            for (int frame = 0; frame < 180 && equipment.CurrentWeapon == null; frame++)
            {
                yield return null;
            }

            WeaponInstance weapon = equipment.CurrentWeapon;
            Assert.That(weapon, Is.Not.Null, "Initial inventory must equip the AK12.");
            Assert.That(weapon.Definition.name, Is.EqualTo("AK12LayerIntegrationTestWeaponDefinition"));
            Assert.That(weapon.IsArmed, Is.True);
            Assert.That(weapon.IsDisposed, Is.False);
            Assert.That(weapon.PresentationRoot, Is.Not.Null, "Equipped AK12 must create its presentation root.");
            GameObject presentationRoot = weapon.PresentationRoot;
            Assert.That(weapon.IsDisposed, Is.False);
            Assert.That(weapon.IsArmed, Is.True);
            Assert.That(weapon.PresentationRoot, Is.SameAs(presentationRoot));
            Assert.That(presentationRoot.transform.localPosition, Is.EqualTo(Vector3.zero),
                "AK12 must inherit IK WeaponBone without a presentation-position compensation.");
            Assert.That(Quaternion.Angle(presentationRoot.transform.localRotation, Quaternion.identity),
                Is.LessThan(0.001f),
                "AK12 must inherit IK WeaponBone without a presentation-rotation compensation.");

            BoneProfile profile = animation.AnimInstance.BoneController.ActiveProfile;
            Assert.That(profile, Is.SameAs(weapon.Definition.ArmedProfile));
            Assert.That(profile.name, Is.EqualTo("AK12LayerIntegrationTestProfile"));
            Assert.That(profile.Layers, Has.Count.EqualTo(8));
            Assert.That(profile.Layers[0], Is.TypeOf<PoseSamplerLayerSettings>());
            Assert.That(profile.Layers[1], Is.TypeOf<AttachHandLayerSettings>());
            Assert.That(profile.Layers[2], Is.TypeOf<ViewLayerSettings>());
            Assert.That(profile.Layers[3], Is.TypeOf<AdsLayerSettings>());
            Assert.That(profile.Layers[4], Is.TypeOf<AdditiveLayerSettings>());
            Assert.That(profile.Layers[5], Is.TypeOf<LookLayerSettings>());
            Assert.That(profile.Layers[6], Is.TypeOf<TurnLayerSettings>());
            Assert.That(profile.Layers[7], Is.TypeOf<IkLayerSettings>());
            Assert.That(animation.AnimInstance.BoneController.IsValid(), Is.True);
            Assert.That(pawn.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(renderer => renderer.enabled && renderer.sharedMesh != null && renderer.sharedMesh.name == "Body"), Is.True,
                "Acceptance Pawn must retain the full Body mesh when no dedicated first-person material is authored.");

            yield return VerifyViewWeightAndPose(animation, profile);
            yield return VerifyMouseLook(animation, controller, pawn, profile);
            yield return VerifyAttachHandMaskAndGraphRebuild(animation, weapon, profile);

            PropertyInfo playablesProperty = typeof(CharacterAnimInstance).GetProperty(
                "PlayablesController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var playables = playablesProperty?.GetValue(animation.AnimInstance) as CharacterPlayablesController;
            Assert.That(playables, Is.Not.Null, "Character animation must expose the Playables controller.");
            Assert.That(animation.AnimInstance.GetCurveValue("WeaponBoneWeight"), Is.EqualTo(0f).Within(0.0001f),
                "The AK12 overlay has no NamedCurve, so PoseSampler must receive the Slot-only zero value.");

            PropertyInfo masterMixerProperty = typeof(CharacterPlayablesController).GetProperty(
                "MasterMixer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(masterMixerProperty, Is.Not.Null, "CharacterPlayablesController must expose MasterMixer.");
            AnimationLayerMixerPlayable masterMixer =
                (AnimationLayerMixerPlayable)masterMixerProperty.GetValue(playables);
            Assert.That(masterMixer.GetInputWeight(1), Is.EqualTo(1f).Within(0.0001f),
                "The equipped AK12 overlay branch must contribute to the final Animator output.");

            Transform weaponParent = presentationRoot.transform.parent;
            Assert.That(weaponParent, Is.Not.Null, "AK12 presentation root must remain parented to IK WeaponBone.");
            Assert.That(weaponParent.name, Is.EqualTo("IK WeaponBone"));

            Transform weaponBoneRight = animation.RigComponent.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "IK WeaponBoneRight");
            AnimationScriptPlayable turnPlayable = FindJobPlayable<TurnJob>(animation.Animator.playableGraph);
            AnimationScriptPlayable lookPlayable = FindJobPlayable<LookJob>(animation.Animator.playableGraph);
            AnimationScriptPlayable viewPlayable = FindJobPlayable<ViewJob>(animation.Animator.playableGraph);
            AnimationScriptPlayable attachPlayable = FindJobPlayable<AttachHandJob>(animation.Animator.playableGraph);
            AnimationScriptPlayable samplerPlayable = FindJobPlayable<PoseSamplerJob>(animation.Animator.playableGraph);
            PoseSamplerJob samplerJob = samplerPlayable.GetJobData<PoseSamplerJob>();
            Assert.That(weaponBoneRight.localPosition.magnitude, Is.GreaterThan(0.05f),
                "PoseSampler initialization must preserve the sampled right-hand-to-weapon calibration offset.");
            Assert.That(
                Vector3.Distance(
                    weaponBoneRight.localPosition,
                    samplerJob.WeaponBoneRightLocalPose.Position),
                Is.LessThan(0.0001f),
                "The AK12 reference must remain a right-hand-local grip offset, not a frozen root-space pose.");
            Assert.That(
                Quaternion.Angle(
                    weaponBoneRight.localRotation,
                    samplerJob.WeaponBoneRightLocalPose.Rotation),
                Is.LessThan(0.05f),
                "The AK12 reference rotation must remain local to the animated right hand.");
            Assert.That(Vector3.Distance(weaponParent.position, weaponBoneRight.position), Is.LessThan(0.001f),
                "With WeaponBoneWeight zero, IK WeaponBone must use the calibrated right-hand reference.");
            Assert.That(Quaternion.Angle(weaponParent.rotation, weaponBoneRight.rotation), Is.LessThan(0.1f),
                "With WeaponBoneWeight zero, IK WeaponBone must use the calibrated right-hand rotation.");

            KVirtualElement[] virtualElements = animation.RigComponent
                .GetComponentsInChildren<KVirtualElement>(true);
            Assert.That(virtualElements, Has.Length.EqualTo(8));
            Assert.That(virtualElements.All(element => element.TargetBone != null), Is.True,
                "Migrated virtual targets must keep their production hand, elbow, foot and knee mappings.");

        }

        private static IEnumerator VerifyMouseLook(
            PawnAnimationComponent animation,
            PlayerController controller,
            Pawn pawn,
            BoneProfile profile)
        {
            LookLayerSettings settings = (LookLayerSettings)profile.Layers[5];;;
            Assert.That(settings.CurveBlending, Has.Length.EqualTo(1));
            AnimationCurveBlend blend = settings.CurveBlending[0];
            Assert.That(blend.CurveName, Is.EqualTo("LookLayerWeight"));
            Assert.That(blend.Source, Is.EqualTo(AnimationCurveBlendSource.Context));
            Assert.That(settings.UseTurnOffset, Is.True);
            VerifyLookElements(settings.PitchElements,
                ("Hips", 5f), ("Spine", 17f), ("Chest", 17f),
                ("UpperChest", 17f), ("Neck", 34f));;
            VerifyLookElements(settings.YawElements,
                ("Spine", 25f), ("Chest", 32.5f), ("UpperChest", 32.5f));
            VerifyLookElements(settings.RollElements,
                ("Hips", 5f), ("Spine", 28.33f), ("Chest", 28.33f), ("UpperChest", 28.33f));

            TurnLayerSettings turnSettings = (TurnLayerSettings)profile.Layers[6];;;
            Assert.That(turnSettings.AngleThreshold, Is.EqualTo(70f).Within(0.001f));
            Assert.That(turnSettings.TurnSpeed, Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(turnSettings.AnimatorTurnLeftTrigger, Is.EqualTo("TurnLeft"));
            Assert.That(turnSettings.AnimatorTurnRightTrigger, Is.EqualTo("TurnRight"));
            Assert.That(animation.Animator.parameters.Any(parameter =>
                parameter.name == turnSettings.AnimatorTurnLeftTrigger
                && parameter.type == AnimatorControllerParameterType.Trigger), Is.True,
                "The production AnimatorControllerPlayable must expose the TurnLeft trigger.");
            Assert.That(animation.Animator.parameters.Any(parameter =>
                parameter.name == turnSettings.AnimatorTurnRightTrigger
                && parameter.type == AnimatorControllerParameterType.Trigger), Is.True,
                "The production AnimatorControllerPlayable must expose the TurnRight trigger.");

            Mouse mouse = InputSystem.AddDevice<Mouse>("Ak12LookMouse");
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(24f, -12f) });
                InputSystem.Update();
                World.Current.UpdateTick(1f / 60f);
                yield return null;

                Assert.That(controller.ControlYaw, Is.GreaterThan(0f), "Mouse delta must reach PlayerController yaw.");
                Assert.That(controller.ControlPitch, Is.GreaterThan(0f), "Mouse delta must reach PlayerController pitch.");
                Assert.That(float.IsFinite(animation.AnimInstance.UpdateContext.ViewAnglesDegrees.x), Is.True,
                    "Control yaw must leave the read-only Look context finite.");
                Assert.That(float.IsFinite(animation.AnimInstance.UpdateContext.ViewAnglesDegrees.y), Is.True,
                    "Control pitch must leave the read-only Look context finite.");

                AnimationScriptPlayable lookPlayable = FindJobPlayable<LookJob>(animation.Animator.playableGraph);
                LookJob job = lookPlayable.GetJobData<LookJob>();
                Assert.That(job.Weight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(float.IsFinite(job.ViewAnglesDegrees.x), Is.True);
                Assert.That(float.IsFinite(job.ViewAnglesDegrees.y), Is.True);
                Assert.That(job.ViewAnglesDegrees.x, Is.InRange(-180f, 180f));
                Assert.That(job.ViewAnglesDegrees.y, Is.InRange(-89f, 89f));
                Assert.That(job.ViewAnglesDegrees.x, Is.EqualTo(
                    animation.AnimInstance.UpdateContext.ViewAnglesDegrees.x
                    + animation.AnimInstance.UpdateContext.TurnOffsetDegrees).Within(0.001f),
                    "Look yaw must combine camera-to-physics-root and physics-root-to-ModelRoot offsets.");

                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(160f, 0f) });
                InputSystem.Update();
                World.Current.UpdateTick(1f / 60f);
                yield return null;

                AnimationScriptPlayable turnPlayable = FindJobPlayable<TurnJob>(animation.Animator.playableGraph);
                TurnJob turnJob = turnPlayable.GetJobData<TurnJob>();
                Assert.That(Mathf.Abs(turnJob.TurnAngleDegrees), Is.GreaterThan(0.1f),
                    "A threshold-crossing Mouse delta must reach the Turn playable through the production input path.");

                Camera camera = pawn.Root.GetComponentInChildren<Camera>(true);
                float idleFov = camera.fieldOfView;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                InputSystem.Update();
                Assert.That(mouse.rightButton.isPressed, Is.True,
                    "The virtual input device must report its right button as held before the production World tick.");
                for (int frame = 0; frame < 45; frame++)
                {
                    World.Current.UpdateTick(1f / 60f);
                    yield return null;
                }

                Assert.That(pawn.IsAiming, Is.True,
                    "Holding the production right mouse button must activate the acceptance AK12 Aim ability.");
                Assert.That(camera.fieldOfView, Is.LessThan(idleFov),
                    "Full ADS must narrow the Camera FOV in the acceptance scene.");
                AnimationScriptPlayable adsPlayable = FindJobPlayable<AdsJob>(animation.Animator.playableGraph);
                Assert.That(adsPlayable.GetJobData<AdsJob>().AimingWeight, Is.GreaterThan(0.99f));

                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                for (int frame = 0; frame < 45; frame++)
                {
                    World.Current.UpdateTick(1f / 60f);
                    yield return null;
                }

                Assert.That(pawn.IsAiming, Is.False,
                    "Releasing the production right mouse button must exit ADS.");
                Assert.That(camera.fieldOfView, Is.EqualTo(idleFov).Within(0.1f));
            }
            finally
            {
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.RemoveDevice(mouse);
            }
        }

        private static IEnumerator VerifyViewWeightAndPose(
            PawnAnimationComponent animation,
            BoneProfile profile)
        {
            ViewLayerSettings settings = (ViewLayerSettings)profile.Layers[2];
            Assert.That(settings.CurveBlending, Has.Length.EqualTo(1));
            AnimationCurveBlend blend = settings.CurveBlending[0];
            Assert.That(blend.CurveName, Is.EqualTo("FullBodyWeight"));
            Assert.That(blend.Mode, Is.EqualTo(AnimationCurveBlendMode.Mask));
            Assert.That(blend.ClampMinimum, Is.Zero.Within(0.0001f));
            Assert.That(blend.Source, Is.EqualTo(AnimationCurveBlendSource.Animator));
            Assert.That(settings.IkWeaponBone.Element.Name, Is.EqualTo("IK WeaponBone"));
            Assert.That(
                Quaternion.Angle(
                    settings.IkWeaponBone.Pose.Rotation,
                    new Quaternion(0.00007449071f, -0.005846337f, 0.0127402f, -0.9999017f)),
                Is.LessThan(0.001f));
            Assert.That(settings.IkWeaponBone.Space, Is.EqualTo(TransformSpace.ComponentSpace));
            Assert.That(settings.IkWeaponBone.ModifyMode, Is.EqualTo(TransformModifyMode.Add));

            int fullBodyWeightHash = Animator.StringToHash("FullBodyWeight");
            Assert.That(
                animation.Animator.parameters.Any(parameter =>
                    parameter.nameHash == fullBodyWeightHash && parameter.type == AnimatorControllerParameterType.Float),
                Is.True,
                "The production Animator must expose the View FullBodyWeight source.");

            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(animation.Animator.GetFloat(fullBodyWeightHash), Is.Zero.Within(0.0001f));
            AnimationScriptPlayable lookPlayable = FindJobPlayable<LookJob>(animation.Animator.playableGraph);
            AnimationScriptPlayable viewPlayable = FindJobPlayable<ViewJob>(animation.Animator.playableGraph);
            ViewJob viewJob = viewPlayable.GetJobData<ViewJob>();
            Assert.That(viewJob.Weight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(viewJob.WeaponPose.Pose.Position, Is.EqualTo(settings.IkWeaponBone.Pose.Position));
            Assert.That(
                Quaternion.Angle(viewJob.WeaponPose.Pose.Rotation, settings.IkWeaponBone.Pose.Rotation),
                Is.LessThan(0.001f));
            Assert.That(viewJob.WeaponPose.Space, Is.EqualTo(TransformSpace.ComponentSpace));
            Assert.That(viewJob.WeaponPose.ModifyMode, Is.EqualTo(TransformModifyMode.Add));
        }

        private static void VerifyLookElements(
            System.Collections.Generic.IReadOnlyList<LookLayerElement> actual,
            params (string bone, float limit)[] expected)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].Element.Name, Is.EqualTo(expected[index].bone));
                Assert.That(actual[index].AngleLimits.x, Is.EqualTo(expected[index].limit).Within(0.001f));
                Assert.That(actual[index].AngleLimits.y, Is.EqualTo(expected[index].limit).Within(0.001f));
            }
        }

        private static IEnumerator VerifyAttachHandMaskAndGraphRebuild(
            PawnAnimationComponent animation,
            WeaponInstance weapon,
            BoneProfile profile)
        {
            CharacterAnimInstance animInstance = animation.AnimInstance;
            AttachHandLayerSettings settings = (AttachHandLayerSettings)profile.Layers[1];
            Assert.That(settings.CurveBlending, Has.Length.EqualTo(1));
            AnimationCurveBlend mask = settings.CurveBlending[0];
            Assert.That(mask.CurveName, Is.EqualTo("MaskAttachHand"));
            Assert.That(mask.Mode, Is.EqualTo(AnimationCurveBlendMode.Mask));
            Assert.That(mask.Source, Is.EqualTo(AnimationCurveBlendSource.Playables));
            Assert.That(settings.OverridePoseWeight, Is.EqualTo(1f).Within(0.0001f),
                "The isolated AttachHand authoring asset must keep override pose weight 1.");

            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(animInstance.GetCurveValue("MaskAttachHand"), Is.EqualTo(0f).Within(0.0001f),
                "The idle Playables source must expose MaskAttachHand=0.");
            AssertAttachHandJobAndPose(animation, settings, 1f, 0.002f, 0.15f, "MaskAttachHand=0");

            AnimationClipAsset curveAsset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            curveAsset.name = "MaskAttachHandOneRuntimeAcceptance";
            var runtimeClip = new AnimationClip { name = "MaskAttachHandOneRuntimeClip" };
            runtimeClip.SetCurve(
                "__AttachHandAcceptanceNeverBinds",
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Constant(0f, 2f, 0f));
            Assert.That(curveAsset.TryInitialize(runtimeClip), Is.True);
            curveAsset.BlendInTime = 0f;
            curveAsset.BlendOutTime = 0f;
            curveAsset.SetNamedCurve("MaskAttachHand", AnimationCurve.Constant(0f, 1f, 1f));
            AnimationPlaybackHandle handle = animInstance.PlayAbilityAnimation(curveAsset, 8008);
            try
            {
                Assert.That(handle, Is.Not.Null, "Acceptance animation slot must return a playback handle.");
                Assert.That(handle.State, Is.Not.EqualTo(AnimationPlaybackState.Failed));
                for (int frame = 0; frame < 4; frame++) yield return null;

                Assert.That(animInstance.GetCurveValue("MaskAttachHand"), Is.EqualTo(1f).Within(0.0001f),
                    "The active slot NamedCurve must expose MaskAttachHand=1.");
                AssertAttachHandJobAndPose(animation, settings, 0f, null, null, "MaskAttachHand=1");

            }
            finally
            {
                animInstance.StopAbilityAnimation(handle);
                UnityEngine.Object.Destroy(curveAsset);
                UnityEngine.Object.Destroy(runtimeClip);
            }

            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(animInstance.GetCurveValue("MaskAttachHand"), Is.EqualTo(0f).Within(0.0001f),
                "Stopping the slot must restore MaskAttachHand=0.");
            AssertAttachHandJobAndPose(animation, settings, 1f, 0.002f, 0.15f,
                "MaskAttachHand=0 after repeated evaluation");

            Assert.That(animInstance.TryRebuildGraph(), Is.True,
                "The production graph must rebuild while the isolated five-layer profile is linked.");
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(animInstance.BoneController.ActiveProfile, Is.SameAs(profile));
            Assert.That(animInstance.BoneController.IsValid(), Is.True);
            Assert.That(animation.Animator.playableGraph.IsValid(), Is.True);
            Assert.That(animInstance.GetCurveValue("MaskAttachHand"), Is.EqualTo(0f).Within(0.0001f),
                "Graph rebuild must restore the idle Playables source MaskAttachHand=0.");
            AssertAttachHandJobAndPose(animation, settings, 1f, 0.002f, 0.15f,
                "MaskAttachHand=0 after graph rebuild");
        }

        private static void AssertAttachHandJobAndPose(
            PawnAnimationComponent animation,
            AttachHandLayerSettings settings,
            float expectedWeight,
            float? maximumPositionError,
            float? maximumRotationError,
            string phase)
        {
            AnimationScriptPlayable lookPlayable = FindJobPlayable<LookJob>(animation.Animator.playableGraph);
            AnimationScriptPlayable viewPlayable = FindJobPlayable<ViewJob>(animation.Animator.playableGraph);
            AnimationScriptPlayable attachPlayable = FindJobPlayable<AttachHandJob>(animation.Animator.playableGraph);
            AttachHandJob job = attachPlayable.GetJobData<AttachHandJob>();
            Assert.That(job.Weight, Is.EqualTo(expectedWeight).Within(0.0001f), phase);

            if (!maximumPositionError.HasValue || !maximumRotationError.HasValue)
            {
                return;
            }

            Transform ikHand = RigHandleUtility.ResolveTransform(
                animation.RigComponent,
                settings.IkHandBone,
                phase);
            Transform ikWeapon = RigHandleUtility.ResolveTransform(
                animation.RigComponent,
                settings.IkWeaponBone,
                phase);
            KTransform relative = job.RelativeHandPose;
            KTransform offset = settings.HandPoseOffset;
            Vector3 expectedPosition = ikWeapon.TransformPoint(relative.Position + offset.Position);
            Quaternion expectedRotation = ikWeapon.rotation * (relative.Rotation * offset.Rotation);
            float positionError = Vector3.Distance(ikHand.position, expectedPosition);
            float rotationError = Quaternion.Angle(ikHand.rotation, expectedRotation);
            Assert.That(positionError, Is.LessThan(maximumPositionError.Value),
                $"{phase} position error was {positionError:F6}m.");
            Assert.That(rotationError, Is.LessThan(maximumRotationError.Value),
                $"{phase} rotation error was {rotationError:F4} degrees.");

            Transform hand = RigHandleUtility.ResolveTransform(
                animation.RigComponent,
                settings.HandBone,
                phase);
            Assert.That(Vector3.Distance(hand.position, ikHand.position), Is.LessThan(0.005f),
                $"{phase} left hand did not reach its IK target.");
            Assert.That(Quaternion.Angle(hand.rotation, ikHand.rotation), Is.LessThan(1f),
                $"{phase} left hand did not match its IK target rotation.");
        }

        private static AnimationScriptPlayable GetFinalProfilePlayable(PlayableGraph graph)
        {
            for (int index = 0; index < graph.GetOutputCount(); index++)
            {
                PlayableOutput output = graph.GetOutput(index);
                if (!output.IsOutputValid()
                    || output.GetPlayableOutputType() != typeof(AnimationPlayableOutput))
                {
                    continue;
                }

                AnimationPlayableOutput animationOutput = (AnimationPlayableOutput)output;
                if (animationOutput.GetAnimationStreamSource() != AnimationStreamSource.PreviousInputs)
                {
                    continue;
                }

                AnimationScriptPlayable blendingPlayable =
                    (AnimationScriptPlayable)animationOutput.GetSourcePlayable();
                return (AnimationScriptPlayable)blendingPlayable.GetInput(0);
            }

            Assert.Fail("Missing CharacterBoneController PreviousInputs output.");
            return AnimationScriptPlayable.Null;
        }

private static AnimationScriptPlayable FindJobPlayable<TJob>(PlayableGraph graph)
            where TJob : struct, IAnimationJob
        {
            for (int outputIndex = 0; outputIndex < graph.GetOutputCount(); outputIndex++)
            {
                PlayableOutput output = graph.GetOutput(outputIndex);
                if (!output.IsOutputValid()) continue;
                AnimationScriptPlayable result = FindJobPlayable<TJob>(output.GetSourcePlayable());
                if (result.IsValid()) return result;
            }

            Assert.Fail($"Missing {typeof(TJob).Name} in CharacterBoneController graph.");
            return AnimationScriptPlayable.Null;
        }

        private static AnimationScriptPlayable FindJobPlayable<TJob>(Playable playable)
            where TJob : struct, IAnimationJob
        {
            if (!playable.IsValid()) return AnimationScriptPlayable.Null;
            if (playable.IsPlayableOfType<AnimationScriptPlayable>())
            {
                AnimationScriptPlayable scriptPlayable = (AnimationScriptPlayable)playable;
                try
                {
                    scriptPlayable.GetJobData<TJob>();
                    return scriptPlayable;
                }
                catch (ArgumentException)
                {
                }
            }

            for (int inputIndex = 0; inputIndex < playable.GetInputCount(); inputIndex++)
            {
                AnimationScriptPlayable result = FindJobPlayable<TJob>(playable.GetInput(inputIndex));
                if (result.IsValid()) return result;
            }

            return AnimationScriptPlayable.Null;
        }





    }
}
