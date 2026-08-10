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
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/SampleScene.unity", LoadSceneMode.Single);

            GameInstance gameInstance = Object.FindObjectOfType<GameInstance>();
            Assert.That(gameInstance, Is.Not.Null, "SampleScene must use the production GameInstance entry point.");
            yield return Await(gameInstance.InitializationTask);
            Assert.That(gameInstance.RuntimeWorld.State, Is.EqualTo(WorldState.Playing));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null && keyboard.added)
            {
                InputSystem.RemoveDevice(keyboard);
            }

            if (mouse != null && mouse.added)
            {
                InputSystem.RemoveDevice(mouse);
            }

            if (World.Current != null)
            {
                yield return Await(World.Current.ShutdownAsync());
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardAndMouse_ReachPawnMotorAnimatorAndCameraThroughProductionScene()
        {
            World world = World.Current;
            var gameMode = (DefaultGameMode)world.GameMode;
            Pawn pawn = gameMode.DefaultPawn;
            var controller = (PlayerController)world.LocalPlayer.Controller;
            PawnMovementComponent movement = pawn.GetComponent<PawnMovementComponent>();
            PawnAnimationComponent animation = pawn.GetComponent<PawnAnimationComponent>();
            PawnCameraComponent camera = pawn.GetComponent<PawnCameraComponent>();
            InputSubSystem input = world.LocalPlayer.GetSubSystem<InputSubSystem>();

            Assert.That(pawn.Root.scene.path, Is.EqualTo("Assets/Scenes/SampleScene.unity"));
            Assert.That(movement.Motor, Is.Not.Null);
            Assert.That(animation.Animator, Is.Not.Null);
            Assert.That(animation.AnimInstance, Is.Not.Null);
            Assert.That(camera.Camera, Is.Not.Null);
            Assert.That(camera.Camera.enabled, Is.True);
            Assert.That(camera.Camera.nearClipPlane, Is.LessThanOrEqualTo(0.05f));
            QueueKeyboard(Key.W);
            for (int frame = 0;
                 frame < 20 && input.ReadControlIntent().MovementInput.z <= 0.9f;
                 frame++)
            {
                yield return null;
            }

            Assert.That(input.ReadControlIntent().MovementInput.z, Is.GreaterThan(0.9f));
            Vector3 initialPosition = pawn.Transform.position;
            yield return FixedFrames(35);
            Assert.That(pawn.Transform.position.z, Is.GreaterThan(initialPosition.z + 0.5f));
            Assert.That(
                animation.Animator.GetBool("Moving"),
                Is.True,
                "CharacterAnimInstance must drive the arms controller's Moving parameter.");
            Assert.That(
                animation.Animator.GetCurrentAnimatorStateInfo(0).IsName("Standing"),
                Is.True,
                "The FPS arms locomotion layer must evaluate its Standing blend tree.");
            Assert.That(animation.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(0f));

            yield return FixedFrames(170);
            float wallStoppedZ = pawn.Transform.position.z;
            Assert.That(wallStoppedZ, Is.InRange(2.8f, 3.4f), "Motor must stop at the wall collider.");
            yield return FixedFrames(20);
            Assert.That(pawn.Transform.position.z, Is.EqualTo(wallStoppedZ).Within(0.03f));

            QueueKeyboard(Key.S);
            yield return FixedFrames(35);
            Assert.That(pawn.Transform.position.z, Is.LessThan(wallStoppedZ - 0.4f));
            float beforeStrafe = pawn.Transform.position.x;
            QueueKeyboard(Key.A);
            yield return FixedFrames(30);
            Assert.That(pawn.Transform.position.x, Is.LessThan(beforeStrafe - 0.25f));
            float afterLeft = pawn.Transform.position.x;
            QueueKeyboard(Key.D);
            yield return FixedFrames(45);
            Assert.That(pawn.Transform.position.x, Is.GreaterThan(afterLeft + 0.4f));

            QueueKeyboard();
            yield return FixedFrames(12);
            Quaternion cameraBeforeLook = camera.Camera.transform.rotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(24f, 12f) });
            yield return null;
            yield return null;
            Assert.That(controller.ControlYaw, Is.GreaterThan(0f));
            Assert.That(controller.ControlPitch, Is.LessThan(0f));
            Assert.That(Quaternion.Angle(cameraBeforeLook, camera.Camera.transform.rotation), Is.GreaterThan(1f));
            Assert.That(camera.Camera.transform.parent.name, Is.EqualTo("Head"));
            Assert.That(camera.Camera.transform.IsChildOf(animation.Animator.transform), Is.True);
            Assert.That(
                Vector3.Distance(
                    camera.Camera.transform.position,
                    camera.Camera.transform.parent.position),
                Is.EqualTo(0.14f).Within(0.02f));

            GameObject root = pawn.Root;
            yield return Await(gameMode.SpawnDefaultPawn(controller));
            yield return null;
            Assert.That(root == null, Is.True, "Pawn-owned Unity Camera must be released with the Pawn root.");
            Assert.That(controller.PossessedPawn, Is.SameAs(gameMode.DefaultPawn));
            Assert.That(gameMode.DefaultPawn.GetComponent<PawnCameraComponent>().Camera.enabled, Is.True);
        }

        private void QueueKeyboard(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static IEnumerator FixedFrames(int count)
        {
            for (int index = 0; index < count; index++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator Await(Task task)
        {
            while (task != null && !task.IsCompleted)
            {
                yield return null;
            }

            if (task != null && task.IsFaulted)
            {
                throw task.Exception.InnerException;
            }
        }
    }
}
