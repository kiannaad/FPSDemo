using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.PawnRuntime.PlayMode.Tests
{
    public sealed class SampleScenePlayableCharacterPlayModeTests
    {
        private Keyboard keyboard;
        private Mouse mouse;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            keyboard = InputSystem.AddDevice<Keyboard>("SampleSceneTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("SampleSceneTestMouse");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            WorldBehaviour behaviour = Object.FindObjectOfType<WorldBehaviour>();
            if (behaviour != null)
            {
                Object.Destroy(behaviour.gameObject);
                yield return null;
            }

            if (World.Current != null)
            {
                Task shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted)
                {
                    yield return null;
                }
            }

            if (keyboard != null && keyboard.added)
            {
                InputSystem.RemoveDevice(keyboard);
            }

            if (mouse != null && mouse.added)
            {
                InputSystem.RemoveDevice(mouse);
            }

            keyboard = null;
            mouse = null;
        }

        [UnityTest]
        public IEnumerator SampleScene_KeyboardMovesVisibleAnimatedPawnAndMouseTurnsCamera()
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                "SampleScene",
                LoadSceneMode.Single);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            WorldBehaviour behaviour = Object.FindObjectOfType<WorldBehaviour>();
            Assert.That(behaviour, Is.Not.Null);
            while (!behaviour.InitializationTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(behaviour.InitializationTask.Result.Succeeded, Is.True,
                behaviour.InitializationTask.Result.Error);
            GameMode gameMode = ((GameManager)behaviour.RuntimeWorld.CurrentGameSession)
                .CurrentGameMode;
            PawnAssembly pawn = gameMode.CurrentPawnAssembly;
            driveWorldFrame(behaviour.RuntimeWorld);
            yield return null;
            Assert.That(pawn, Is.Not.Null);
            SkinnedMeshRenderer firstPersonRenderer =
                pawn.Root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(firstPersonRenderer, Is.Not.Null);
            Assert.That(firstPersonRenderer.sharedMesh.subMeshCount, Is.EqualTo(1),
                "The first-person Pawn must not render the body or legs submeshes.");
            Assert.That(firstPersonRenderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(firstPersonRenderer.sharedMaterial.name, Is.EqualTo("M_Armature_Arms"));
            Assert.That(Vector3.Distance(
                behaviour.PlayerCamera.transform.position,
                pawn.Root.transform.position + Vector3.up * 1.6f), Is.LessThan(0.02f),
                "The Player camera left the character capsule center line.");
            Assert.That(pawn.Animation.Animator, Is.Not.Null);
            Assert.That(pawn.Animation.Animator.runtimeAnimatorController?.name,
                Is.EqualTo("FPSAnimator_Generic"));
            Assert.That(pawn.Animation.Animator.playableGraph.IsValid(), Is.True,
                "The Animator PlayableGraph was not created after the Pawn became active.");
            Assert.That(pawn.Animation.AnimInstance, Is.Not.Null);

            Vector3 initialPosition = pawn.Root.transform.position;
            Quaternion initialCameraRotation = behaviour.PlayerCamera.transform.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputWorldCoreService inputService = behaviour.RuntimeWorld
                .GetCoreService<InputWorldCoreService>();
            int inputTickCount = inputService.TickCount;
            driveWorldFrame(behaviour.RuntimeWorld);
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard.wKey.isPressed, Is.True,
                "The queued test keyboard state was not retained by the normal Input PlayerLoop.");
            Assert.That(inputService.TickCount, Is.GreaterThan(inputTickCount),
                "The World did not tick its Input core service. Faults: "
                + formatFaults(behaviour.RuntimeWorld));
            Assert.That(inputService.ReadControlIntent().MovementInput.z, Is.GreaterThan(0.5f),
                "The Input core service did not capture the held W key.");
            Assert.That(pawn.Pawn.PeekingMovementInput().z, Is.GreaterThan(0.5f),
                "The real Player input path did not reach the possessed Pawn.");
            for (int frame = 0; frame < 30; frame++)
            {
                driveWorldFrame(behaviour.RuntimeWorld);
                yield return null;
            }

            Assert.That(pawn.Movement.Motor.Velocity.z, Is.GreaterThan(0.01f),
                "The Pawn intent reached gameplay but did not reach the character motor.");
            Assert.That(pawn.Root.transform.position.z,
                Is.GreaterThan(initialPosition.z + 0.05f));
            Assert.That(pawn.Animation.Animator.GetBool("Moving"), Is.True,
                "The locomotion Animator did not enter its moving state.");
            Assert.That(pawn.Animation.Animator.GetFloat("MoveY"), Is.GreaterThan(0.1f));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(90f, 10f));
            driveWorldFrame(behaviour.RuntimeWorld);
            Assert.That(Quaternion.Angle(
                Quaternion.identity,
                gameMode.LocalPlayerController.ControlRotation), Is.GreaterThan(0.1f),
                "The mouse delta did not reach the PlayerController control rotation.");
            Vector3 cameraForward = gameMode.LocalPlayerController.ControlRotation * Vector3.forward;
            Assert.That(cameraForward.x, Is.GreaterThan(0.9f),
                "Moving the mouse right did not turn the controller to the right.");
            Assert.That(cameraForward.y, Is.GreaterThan(0.1f),
                "Moving the mouse up did not pitch the controller upward.");
            yield return null;
            Assert.That(Quaternion.Angle(
                initialCameraRotation,
                behaviour.PlayerCamera.transform.rotation), Is.GreaterThan(0.1f));

            Vector3 positionBeforeCameraRelativeMove = pawn.Root.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (int frame = 0; frame < 30; frame++)
            {
                driveWorldFrame(behaviour.RuntimeWorld);
                yield return null;
            }

            Vector3 cameraRelativeDisplacement =
                pawn.Root.transform.position - positionBeforeCameraRelativeMove;
            Assert.That(cameraRelativeDisplacement.x,
                Is.GreaterThan(Mathf.Abs(cameraRelativeDisplacement.z)),
                "W did not move primarily along the camera's turned forward direction.");
        }

        [UnityTest]
        public IEnumerator SampleScene_ObstacleStopsPawnAndCameraUsesFirstPersonNearClip()
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                "SampleScene",
                LoadSceneMode.Single);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            WorldBehaviour behaviour = Object.FindObjectOfType<WorldBehaviour>();
            Assert.That(behaviour, Is.Not.Null);
            while (!behaviour.InitializationTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(behaviour.InitializationTask.Result.Succeeded, Is.True,
                behaviour.InitializationTask.Result.Error);
            GameMode gameMode = ((GameManager)behaviour.RuntimeWorld.CurrentGameSession)
                .CurrentGameMode;
            PawnAssembly pawn = gameMode.CurrentPawnAssembly;
            Collider obstacle = GameObject.Find("ForwardMarker")?.GetComponent<Collider>();
            Assert.That(obstacle, Is.Not.Null);
            Assert.That(obstacle.enabled, Is.True,
                "The visible forward obstacle must participate in collision.");
            Assert.That(behaviour.PlayerCamera.nearClipPlane, Is.LessThanOrEqualTo(0.03f),
                "The first-person camera near plane is large enough to cut through nearby walls.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (int frame = 0; frame < 240; frame++)
            {
                driveWorldFrame(behaviour.RuntimeWorld);
                yield return null;
            }

            Assert.That(pawn.Root.transform.position.z, Is.LessThan(obstacle.bounds.min.z),
                "The character motor crossed through the forward obstacle.");
            Assert.That(pawn.Movement.Motor.Capsule.bounds.Intersects(obstacle.bounds), Is.False,
                "The character capsule remained embedded in the forward obstacle.");
        }

        private static string formatFaults(World world)
        {
            var lines = new System.Collections.Generic.List<string>();
            foreach (TickFault fault in world.TickScheduler.Faults)
            {
                lines.Add($"{fault.Handle.Name}: {fault.Exception}");
            }

            return string.Join(" | ", lines);
        }

        private static void driveWorldFrame(World world)
        {
            InputSystem.Update();
            world.UpdateTick(1f / 60f);
            world.FixedTick(1f / 50f);
            world.LateTick(1f / 60f, Time.time);
        }
    }
}
