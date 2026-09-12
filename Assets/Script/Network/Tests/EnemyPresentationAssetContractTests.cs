using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyPresentationAssetContractTests
    {
        [TestCase("Pistol")]
        [TestCase("Rifle")]
        [TestCase("Ak")]
        public void Fire_KeepsAimedTorsoStableWhileHandsAndWeaponRecoilTogether(string variant)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy{variant}.prefab");
            var root = Object.Instantiate(prefab);
            var controlRoot = Object.Instantiate(prefab);
            try
            {
                var presentation = root.GetComponent<EnemyPresentation>();
                var control = controlRoot.GetComponent<EnemyPresentation>();
                presentation.Animator.Rebind();
                control.Animator.Rebind();
                void Advance()
                {
                    presentation.ApplyRemoteAnimationState(new RemoteEnemyAnimationState(
                        0f, Vector2.zero, true, Quaternion.identity, EnemyBrainState.Fire), 1f / 60f);
                    presentation.Animator.playableGraph.Evaluate(1f / 60f);
                    control.ApplyRemoteAnimationState(new RemoteEnemyAnimationState(
                        0f, Vector2.zero, true, Quaternion.identity, EnemyBrainState.Fire), 1f / 60f);
                    control.Animator.playableGraph.Evaluate(1f / 60f);
                }
                for (int frame = 0; frame < 60; frame++) Advance();
                Transform spine = presentation.Animator.GetBoneTransform(HumanBodyBones.Spine);
                Transform hand = presentation.Animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform muzzle = root.GetComponentsInChildren<Transform>().First(value => value.name == "muzzle");
                Transform controlSpine = control.Animator.GetBoneTransform(HumanBodyBones.Spine);
                Quaternion weaponPose = muzzle.rotation;
                Vector3 grip = muzzle.InverseTransformPoint(hand.position);
                float torsoExcursion = 0f;
                float weaponExcursion = 0f;
                float gripDrift = 0f;
                presentation.PlayFire();
                for (int frame = 0; frame < 90; frame++)
                {
                    Advance();
                    torsoExcursion = Mathf.Max(torsoExcursion, Quaternion.Angle(controlSpine.rotation, spine.rotation));
                    weaponExcursion = Mathf.Max(weaponExcursion, Quaternion.Angle(weaponPose, muzzle.rotation));
                    gripDrift = Mathf.Max(gripDrift, Vector3.Distance(grip, muzzle.InverseTransformPoint(hand.position)));
                }
                Assert.That(torsoExcursion, Is.LessThan(3f), "Shooting must not swing the aimed torso independently of the gun.");
                Assert.That(weaponExcursion, Is.GreaterThan(.5f), "Do not fix recoil by freezing the firing pose.");
                Assert.That(gripDrift, Is.LessThan(.03f), "The supporting hand must remain attached during recoil.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(controlRoot);
            }
        }

        [TestCase("Pistol")]
        [TestCase("Rifle")]
        [TestCase("Ak")]
        public void WeaponPose_SeparatesTravelAimFireAndReturn(string variant)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy{variant}.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var presentation = root.GetComponent<EnemyPresentation>();
                void Advance(float speed, EnemyBrainState state, int frames = 30)
                {
                    for (int frame = 0; frame < frames; frame++)
                    {
                        presentation.ApplyRemoteAnimationState(new RemoteEnemyAnimationState(
                            speed, speed > .05f ? Vector2.up : Vector2.zero, true,
                            Quaternion.identity, state), 1f / 60f);
                        presentation.Animator.playableGraph.Evaluate(1f / 60f);
                    }
                }
                Advance(2f, EnemyBrainState.Chase);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                object playback = typeof(EnemyPresentation).GetField("playablesController", flags).GetValue(presentation);
                var source = (UnityEngine.Animations.AnimatorControllerPlayable)playback.GetType()
                    .GetField("animatorControllerSource", flags).GetValue(playback);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Guard"), Is.True,
                    "Travel must use the original lowered guard pose, not an always-on aim overlay.");
                Advance(0f, EnemyBrainState.Fire);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Aim"), Is.True);
                presentation.PlayFire();
                Advance(0f, EnemyBrainState.Fire, 6);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Fire"), Is.True);
                Advance(2f, EnemyBrainState.ReturnToCover);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Guard"), Is.True,
                    "Returning to cover must interrupt the firing pose.");
                presentation.PlayFire();
                Advance(2f, EnemyBrainState.PeekFire);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Guard"), Is.True,
                    "A delayed shot event must not raise the gun while travelling to a peek point.");
                Advance(0f, EnemyBrainState.PeekFire, 12);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Fire"), Is.False,
                    "A shot suppressed during travel must not remain queued for the next stop.");
                Advance(0f, EnemyBrainState.PeekFire);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Aim"), Is.True);
                presentation.PlayHit();
                Advance(2f, EnemyBrainState.ReturnToCover, 12);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Hit"), Is.True,
                    "Separating aim from movement must preserve the hit reaction.");
                Advance(2f, EnemyBrainState.ReturnToCover, 90);
                Assert.That(source.GetCurrentAnimatorStateInfo(1).IsName("Weapon.Guard"), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Pistol", 0f, 60)]
        [TestCase("Rifle", 0f, 60)]
        [TestCase("Ak", 0f, 60)]
        [TestCase("Pistol", 35f, 30)]
        [TestCase("Rifle", 35f, 30)]
        [TestCase("Ak", 35f, 30)]
        [TestCase("Pistol", 90f, 144)]
        [TestCase("Rifle", 90f, 144)]
        [TestCase("Ak", 90f, 144)]
        [TestCase("Pistol", 175f, 59)]
        [TestCase("Rifle", 175f, 59)]
        [TestCase("Ak", 175f, 59)]
        public void RenderedDeathBody_RestsAtAuthorityFootPlane(string variant, float yaw, int frameRate)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy{variant}.prefab");
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            var root = Object.Instantiate(prefab, Vector3.zero, rotation);
            rotation = root.transform.rotation;
            try
            {
                var presentation = root.GetComponent<EnemyPresentation>();
                presentation.Animator.Rebind();
                presentation.ApplyRemoteAnimationState(new RemoteEnemyAnimationState(
                    0f, Vector2.zero, true, Quaternion.identity, EnemyBrainState.Fire), .1f);
                presentation.Animator.playableGraph.Evaluate(.5f);
                for (int hit = 0; hit < 4; hit++)
                {
                    presentation.PlayHit();
                    for (int frame = 0; frame < frameRate; frame++)
                    {
                        presentation.TickPresentation(.8f / frameRate);
                        presentation.Animator.playableGraph.Evaluate(.8f / frameRate);
                    }
                }
                presentation.PlayDeath();
                for (int frame = 0; frame < frameRate; frame++)
                {
                    presentation.TickPresentation(1f / frameRate);
                    presentation.Animator.playableGraph.Evaluate(1f / frameRate);
                }
                float lowest = float.PositiveInfinity;
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh();
                    try
                    {
                        renderer.BakeMesh(mesh);
                        foreach (Vector3 vertex in mesh.vertices)
                            lowest = Mathf.Min(lowest, renderer.transform.TransformPoint(vertex).y);
                    }
                    finally { Object.DestroyImmediate(mesh); }
                }
                Debug.Log($"[Lab068] DeathGroundContract {variant} LowestVertexY={lowest:F4}");
                Assert.That(lowest, Is.EqualTo(root.transform.position.y).Within(.03f),
                    "The baked corpse must neither penetrate nor float above its authority foot plane.");
                Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(root.transform.rotation, Is.EqualTo(rotation));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Pistol")]
        [TestCase("Rifle")]
        [TestCase("Ak")]
        public void DeathBesideCover_FallsIntoClearSpaceWithoutMovingNetworkRoot(string variant)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy{variant}.prefab");
            // Keep this isolated collider contract outside the open scene's
            // geometry (the arena stairs now occupy the world origin).
            Vector3 origin = new Vector3(1000f, 0f, 1000f);
            var root = Object.Instantiate(prefab, origin, Quaternion.identity);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = origin + new Vector3(1f, 1f, 0f);
                wall.transform.localScale = new Vector3(0.5f, 2f, 4f);
                Physics.SyncTransforms();
                var presentation = root.GetComponent<EnemyPresentation>();
                presentation.PlayDeath();
                presentation.TickPresentation(1f);
                Vector3 fallDirection = Vector3.ProjectOnPlane(presentation.VisualRoot.up, Vector3.up).normalized;
                Assert.That(Vector3.Dot(fallDirection, Vector3.right), Is.LessThan(0.1f),
                    "The death silhouette must not topple into the adjacent cover.");
                Assert.That(Physics.OverlapCapsule(origin + Vector3.up * 0.4f,
                    origin + Vector3.up * 0.4f + fallDirection * 1.6f, 0.3f).Contains(wall.GetComponent<Collider>()), Is.False);
                Assert.That(Quaternion.Angle(Quaternion.identity, presentation.VisualRoot.localRotation), Is.GreaterThan(60f));
                Assert.That(root.transform.position, Is.EqualTo(origin));
                Assert.That(root.transform.rotation, Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(wall);
            }
        }

        [TestCase("Pistol")]
        [TestCase("Rifle")]
        [TestCase("Ak")]
        public void RenderedAimPose_PointsMuzzleAlongEnemyFacing(string variant)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy{variant}.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var presentation = root.GetComponent<EnemyPresentation>();
                presentation.Animator.Rebind();
                presentation.Animator.Update(0f);
                // Advance actual frames through Guard -> Aim; one large first
                // evaluation only starts an Animator transition.
                for (int frame = 0; frame < 30; frame++)
                {
                    presentation.ApplyRemoteAnimationState(new RemoteEnemyAnimationState(
                        0f, Vector2.zero, true, Quaternion.identity, EnemyBrainState.Fire), 1f / 60f);
                    presentation.Animator.playableGraph.Evaluate(1f / 60f);
                }
                Transform muzzle = root.GetComponentsInChildren<Transform>(true).Single(value => value.name == "muzzle");
                Vector3 handToMuzzle = muzzle.position - presentation.Animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                // TPS Bundle authored its barrel along the muzzle's negative X axis.
                Vector3 barrelDirection = -muzzle.right;
                Debug.Log($"[Lab068] AimContract {variant} BarrelLocal={root.transform.InverseTransformDirection(barrelDirection)} HandToMuzzleLocal={root.transform.InverseTransformDirection(handToMuzzle.normalized)}");
                Assert.That(Vector3.Dot(barrelDirection, root.transform.forward), Is.GreaterThan(0.98f),
                    "The rendered weapon must aim in the same direction as the authority-facing root.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Pistol")]
        [TestCase("Rifle")]
        [TestCase("Ak")]
        public void Prefab_HasLocalLocomotionWeaponLayerAndIsolatedVisualRoot(string variant)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy{variant}.prefab");
            Assert.That(prefab, Is.Not.Null);
            Debug.Log($"[DeathPhysics] {variant} " + string.Join(";", prefab.GetComponentsInChildren<Rigidbody>(true)
                .Select(body => $"{body.name} kinematic={body.isKinematic} gravity={body.useGravity}")));
            foreach (var body in prefab.GetComponentsInChildren<Rigidbody>(true))
            {
                Assert.That(body.isKinematic, Is.True, "Replicated presentation bones must not be driven by local ragdoll physics.");
                Assert.That(body.useGravity, Is.False, "Only the server motor owns enemy movement.");
            }
            var presentation = prefab.GetComponent<EnemyPresentation>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.VisualRoot, Is.Not.Null);
            Assert.That(presentation.VisualRoot, Is.Not.EqualTo(prefab.transform));
            Animator animator = presentation.Animator;
            Assert.That(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.transform.IsChildOf(presentation.VisualRoot), Is.True);
            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            foreach (string parameter in new[] { "Speed", "MoveX", "MoveY", "IsInCover", "IsPeeking", "Fire", "Hit" })
                Assert.That(controller.parameters.Any(value => value.name == parameter), Is.True, parameter);
            string[] states = controller.layers[0].stateMachine.states.Select(value => value.state.name).ToArray();
            Assert.That(states, Does.Contain("Locomotion"));
            Assert.That(states, Does.Contain("Cover"));
            Assert.That(states, Does.Contain("Peek"));
            Assert.That(controller.layers.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(controller.layers[1].avatarMask, Is.Not.Null);
            AvatarMask weaponMask = controller.layers[1].avatarMask;
            Assert.That(weaponMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.True);
            Assert.That(weaponMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm), Is.True);
            Assert.That(weaponMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
            Assert.That(weaponMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
            Assert.That(weaponMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.False);
            Transform muzzle = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "muzzle");
            Assert.That(muzzle.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightHand)), Is.True);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .All(component => component != null && component.GetType().Namespace == "CGame.Network"), Is.True);
        }
    }
}
