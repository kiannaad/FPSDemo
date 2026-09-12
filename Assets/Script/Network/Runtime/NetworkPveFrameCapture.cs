using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace CGame.Network
{
    /// <summary>Opt-in real-time framebuffer evidence; never changes the simulation clock.</summary>
    public sealed class NetworkPveFrameCapture : MonoBehaviour
    {
        private StreamWriter timestamps;
        private double nextDeathDiagnosticAt;

        public static bool IsEnabledByCommandLine() =>
            Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "-network-pve-frame-directory");

        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-network-pve-frame-directory");
            if (index < 0 || index + 1 >= arguments.Length) yield break;
            string directory = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(directory);
            // A reused run must not overwrite its original evidence.
            timestamps = new StreamWriter(new FileStream(Path.Combine(directory, "timestamps.csv"), FileMode.CreateNew));
            timestamps.WriteLine("frame,seconds,utc");
            while (GetComponent<GameInstance>()?.RuntimeWorld?.IsGameplayReady != true) yield return null;
            double startedAt = Time.realtimeSinceStartupAsDouble;
            double nextFrameAt = startedAt;
            int frame = 0;
            var endOfFrame = new WaitForEndOfFrame();
            while (Time.realtimeSinceStartupAsDouble - startedAt < 65d)
            {
                yield return endOfFrame;
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < nextFrameAt) continue;
                string capturedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                if (now >= nextDeathDiagnosticAt)
                {
                    nextDeathDiagnosticAt = now + .25d;
                    foreach (EnemyPresentation enemy in FindObjectsOfType<EnemyPresentation>())
                    {
                        if (!enemy.IsDead || enemy.Animator == null || !enemy.Animator.isHuman) continue;
                        Transform hips = enemy.Animator.GetBoneTransform(HumanBodyBones.Hips);
                        Transform head = enemy.Animator.GetBoneTransform(HumanBodyBones.Head);
                        float upright = Mathf.Abs(Vector3.Dot((head.position - hips.position).normalized, Vector3.up));
                        Debug.Log($"[Network][077] DeathFrame Frame={frame} Utc={capturedAt} Name={enemy.name} Root={enemy.transform.position:F3} VisualRotation={enemy.VisualRoot.localEulerAngles:F3} Hips={hips.position:F3} Head={head.position:F3} Upright={upright:F3} Complete={enemy.IsDeathPresentationComplete}");
                    }
                }
                Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    byte[] bytes = texture.EncodeToJPG(90);
                    string fileName = frame.ToString("D5", CultureInfo.InvariantCulture) + ".jpg";
                    File.WriteAllBytes(Path.Combine(directory, fileName), bytes);
                    timestamps.WriteLine(frame + "," + (now - startedAt).ToString("F6", CultureInfo.InvariantCulture) + "," + capturedAt);
                }
                finally { Destroy(texture); }
                frame++;
                nextFrameAt = startedAt + Math.Floor((now - startedAt) * 30d + 1d) / 30d;
            }
            timestamps.Dispose();
            timestamps = null;
            Debug.Log($"[Network][070] PveFrameCapture Frames={frame} Seconds={Time.realtimeSinceStartupAsDouble - startedAt:F3}");
        }

        private void OnDestroy()
        {
            timestamps?.Dispose();
            timestamps = null;
        }
    }
}
