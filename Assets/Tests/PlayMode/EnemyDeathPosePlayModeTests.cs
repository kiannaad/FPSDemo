using System.Collections;
using CGame.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class EnemyDeathPosePlayModeTests
    {
        [UnityTest]
        public IEnumerator AutomaticAnimationEvaluation_KeepsDeathBodyAtFootPlane()
        {
            var cameraRoot = new GameObject("DeathPoseFixtureCamera", typeof(Camera));
            cameraRoot.transform.position = new Vector3(0f, 1f, -5f);
            cameraRoot.GetComponent<Camera>().depth = 100f;
            try
            {
                foreach (string variant in new[] { "Pistol", "Rifle", "Ak" })
                for (int timing = 0; timing < 4; timing++)
                {
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy{variant}.prefab");
                    var root = Object.Instantiate(prefab, Vector3.zero, Quaternion.Euler(0f, timing * 45f, 0f));
                    try
                    {
                        var presentation = root.GetComponent<EnemyPresentation>();
                        yield return null;
                        presentation.ApplyRemoteAnimationState(new RemoteEnemyAnimationState(
                            0f, Vector2.zero, true, root.transform.rotation, EnemyBrainState.PeekFire), .1f);
                        presentation.PlayFire();
                        yield return new WaitForSeconds(.3f);
                        presentation.PlayHit();
                        yield return new WaitForSeconds(.17f + timing * .07f);
                        presentation.PlayDeath();
                        float elapsed = 0f;
                        bool loggedFreeze = false;
                        while (elapsed < 1.3f)
                        {
                            presentation.TickPresentation(Time.deltaTime);
                            elapsed += Time.deltaTime;
                            Transform hips = presentation.Animator.GetBoneTransform(HumanBodyBones.Hips);
                            Vector3 beforeHips = hips.localPosition;
                            Quaternion beforeRotation = hips.localRotation;
                            yield return null; // Let Unity evaluate animation automatically.
                            if (!loggedFreeze && elapsed >= .2f)
                            {
                                loggedFreeze = true;
                                Debug.Log($"[DeathAuto] Root={root.transform.position} Visual={presentation.VisualRoot.localPosition} Playing={presentation.Animator.playableGraph.IsPlaying()} HipsDelta={Vector3.Distance(beforeHips, hips.localPosition):F6} HipsAngle={Quaternion.Angle(beforeRotation, hips.localRotation):F6}");
                            }
                            if (elapsed < .9f) continue;
                            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero), "Fixture root must remain unchanged.");
                            float lowest = getLowestVertex(root);
                            if (Mathf.Abs(lowest) > .03f)
                                Debug.Log($"[DeathAuto] Lowest={lowest:F6} Root={root.transform.position} Visual={presentation.VisualRoot.localPosition} AnimatorLocal={presentation.Animator.transform.localPosition} Hips={hips.localPosition}");
                            Assert.That(lowest, Is.EqualTo(0f).Within(.03f),
                                $"{variant} timing={timing} elapsed={elapsed:F3} must retain grounded frozen pose.");
                            Vector3 torso = presentation.Animator.GetBoneTransform(HumanBodyBones.Head).position - hips.position;
                            Assert.That(Mathf.Abs(Vector3.Dot(torso.normalized, Vector3.up)), Is.LessThan(.5f),
                                $"{variant} timing={timing} must visibly lie down, not merely touch the ground with a foot.");
                        }
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            finally { Object.DestroyImmediate(cameraRoot); }
        }

        private static float getLowestVertex(GameObject root)
        {
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
            return lowest;
        }
    }
}
