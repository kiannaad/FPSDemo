using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;

namespace CGame.Tests
{
    public sealed class AICharacterRuntimePlayModeTests
    {
        private GameObject ground;
        private GameObject target;
        private GameObject navigationObstacle;
        private GameObject acceptanceCameraObject;
        private GameObject acceptanceLightObject;
        private readonly List<GameObject> acceptanceDebugObjects = new List<GameObject>();
        private NavMeshData navigationData;
        private NavMeshDataInstance navigationDataInstance;

        [SetUp]
        public void SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "AICharacterRuntimeGround";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
        }

        [TearDown]
        public void TearDown()
        {
            DestroyIfPresent("[GameManager]");
            DestroyIfPresent("[CharacterRuntimeRoot]");
            DestroyObject(target);
            DestroyObject(navigationObstacle);
            DestroyObject(acceptanceCameraObject);
            DestroyObject(acceptanceLightObject);
            for (int i = 0; i < acceptanceDebugObjects.Count; i++)
            {
                DestroyObject(acceptanceDebugObjects[i]);
            }
            acceptanceDebugObjects.Clear();
            DestroyObject(ground);
            navigationDataInstance.Remove();
            DestroyObject(navigationData);
            target = null;
            ground = null;
            navigationObstacle = null;
            acceptanceCameraObject = null;
            acceptanceLightObject = null;
            navigationData = null;
        }

        [UnityTest]
        public IEnumerator AIPerception_VisualSoundDamageAndOccludedMemoryStayFair()
        {
            yield return CharacterSpawnTestConfiguration
                .EnsureResourcesReady();
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithYooAssetDefinitions();
            object operation = Invoke(spawnManager, "BeginSpawn", CreateAIRequest("perception-ai", "PerceptionAI", Vector3.zero));
            yield return AdvanceSpawn(spawnManager, 6);
            Assert.AreEqual("CharacterReady", GetProperty<object>(operation, "State").ToString());

            object runtime = GetAIRuntime(spawnManager, GetProperty<object>(operation, "RuntimeId"));
            object perception = GetProperty<object>(runtime, "Perception");
            Assert.NotNull(perception, "Perception runtime was not attached to the formal AI registration.");
            Transform aiTransform = GetProperty<Transform>(runtime, "Transform");

            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "AIPerceptionTarget";
            target.transform.position = aiTransform.position + aiTransform.forward * 8f + Vector3.up * 1.6f;
            Component perceptionTarget = target.AddComponent(RequireRuntimeType("CGame.AIPerceptionTargetBehaviour"));
            Invoke(perceptionTarget, "Configure", "player", null);
            Assert.IsTrue((bool)Invoke(perception, "RegisterTarget", perceptionTarget));
            Physics.SyncTransforms();
            object lineOfSightQuery = GetField(perception, "lineOfSightQuery");
            Assert.IsTrue((bool)Invoke(
                lineOfSightQuery,
                "HasLineOfSight",
                aiTransform.position + Vector3.up * 1.6f,
                aiTransform,
                perceptionTarget,
                (LayerMask)~0), "The unobstructed target was blocked before runtime sampling.");

            Invoke(perception, "Tick", 100d);
            Invoke(perception, "Tick", 100.3d);
            object visualRecord = FindMemoryRecord(perception, "Visual");
            Assert.NotNull(visualRecord, "Visual memory was not recognized in front of the AI.");
            Vector3 lastVisiblePosition = GetProperty<Vector3>(visualRecord, "LastKnownPosition");
            Assert.AreEqual(target.transform.position, lastVisiblePosition);
            Assert.IsTrue(GetProperty<bool>(visualRecord, "IsPrecise"));

            navigationObstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            navigationObstacle.name = "AIPerceptionOccluder";
            navigationObstacle.transform.position = aiTransform.position + aiTransform.forward * 4f + Vector3.up * 1.6f;
            navigationObstacle.transform.rotation = aiTransform.rotation;
            navigationObstacle.transform.localScale = new Vector3(20f, 3f, 0.5f);
            target.transform.position += aiTransform.right * 4f;
            Physics.SyncTransforms();
            Invoke(perception, "Tick", 100.5d);

            visualRecord = FindMemoryRecord(perception, "Visual");
            Assert.NotNull(visualRecord, "Occlusion unexpectedly removed visual memory before expiry.");
            Assert.AreEqual(lastVisiblePosition, GetProperty<Vector3>(visualRecord, "LastKnownPosition"));
            Assert.AreNotEqual(target.transform.position, GetProperty<Vector3>(visualRecord, "LastKnownPosition"));

            Invoke(perception, "PublishSound", "sound-source", new Vector3(-8f, 0f, -8f), 101d, 5f);
            Invoke(perception, "Tick", 101.1d);
            object soundRecord = FindMemoryRecord(perception, "Sound");
            Assert.NotNull(soundRecord, "Queued sound stimulus was not consumed.");
            Assert.IsFalse(GetProperty<bool>(soundRecord, "IsPrecise"));
            Assert.AreEqual(5f, GetProperty<float>(soundRecord, "UncertaintyRadius"));

            object health = GetProperty<object>(runtime, "Health");
            object damageEvent = Activator.CreateInstance(
                RequireRuntimeType("CGame.DamageEvent"),
                "perception-hit",
                "attacker",
                GetProperty<object>(runtime, "RuntimeId").GetType().GetProperty("Value").GetValue(GetProperty<object>(runtime, "RuntimeId")),
                1f,
                new Vector3(0f, 1f, -1f),
                Vector3.right,
                102d);
            Assert.IsTrue((bool)Invoke(health, "ApplyDamage", damageEvent));
            Invoke(perception, "Tick", 102.1d);
            object damageRecord = FindMemoryRecord(perception, "Damage");
            Assert.NotNull(damageRecord, "Health damage event did not produce a perception memory.");
            Assert.Greater(Vector3.Dot(GetProperty<Vector3>(damageRecord, "Direction"), Vector3.right), 0.999f);

            object beforeSnapshot = Invoke(perception, "CreateDebugSnapshot");
            object afterSnapshot = Invoke(perception, "CreateDebugSnapshot");
            Assert.AreEqual(
                GetProperty<int>(beforeSnapshot, "PendingStimulusCount"),
                GetProperty<int>(afterSnapshot, "PendingStimulusCount"));
            Assert.DoesNotThrow(() => Invoke(perception, "Shutdown"));
            Assert.DoesNotThrow(() => Invoke(perception, "Shutdown"));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AIAlertDecision_FixedClockCompletesAlertLoopAndDeclaredInterrupts()
        {
            yield return CharacterSpawnTestConfiguration
                .EnsureResourcesReady();
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithYooAssetDefinitions();
            object operation = Invoke(spawnManager, "BeginSpawn", CreateAIRequest("decision-ai", "DecisionAI", Vector3.zero));
            yield return AdvanceSpawn(spawnManager, 6);
            Assert.AreEqual("CharacterReady", GetProperty<object>(operation, "State").ToString());

            object runtime = GetAIRuntime(spawnManager, GetProperty<object>(operation, "RuntimeId"));
            object perception = GetProperty<object>(runtime, "Perception");
            object decision = GetProperty<object>(runtime, "Decision");
            Assert.NotNull(perception);
            Assert.NotNull(decision, "Decision runtime was not attached to the formal AI registration.");
            Transform aiTransform = GetProperty<Transform>(runtime, "Transform");

            Invoke(decision, "Tick", 100d);
            Assert.AreEqual("Patrol", GetProperty<object>(decision, "State").ToString());

            Vector3 soundPosition = aiTransform.position + aiTransform.forward * 6f;
            Invoke(perception, "PublishSound", "decision-sound", soundPosition, 101d, 4f);
            Invoke(perception, "Tick", 101d);
            Invoke(decision, "Tick", 101d);
            Assert.AreEqual("Investigate", GetProperty<object>(decision, "State").ToString());

            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "AIDecisionTarget";
            target.transform.position = aiTransform.position + aiTransform.forward * 8f + Vector3.up * 1.6f;
            Component perceptionTarget = target.AddComponent(RequireRuntimeType("CGame.AIPerceptionTargetBehaviour"));
            Invoke(perceptionTarget, "Configure", "decision-player", null);
            Assert.IsTrue((bool)Invoke(perception, "RegisterTarget", perceptionTarget));
            Physics.SyncTransforms();
            Invoke(perception, "Tick", 102d);
            Invoke(perception, "Tick", 102.3d);
            Invoke(decision, "Tick", 102.3d);
            Assert.AreEqual("Combat", GetProperty<object>(decision, "State").ToString());

            object combatExecution = GetProperty<object>(decision, "CurrentExecution");
            Assert.NotNull(combatExecution);
            Invoke(decision, "NotifyMajorDamage", 102.4d);
            object coverCombat = GetProperty<object>(runtime, "CoverCombat");
            Assert.NotNull(coverCombat);
            Assert.AreEqual("Reposition", GetProperty<object>(coverCombat, "Action").ToString());
            Invoke(decision, "Tick", 102.4d);
            Assert.AreEqual("Combat", GetProperty<object>(decision, "State").ToString());

            navigationObstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            navigationObstacle.name = "AIDecisionOccluder";
            navigationObstacle.transform.position = aiTransform.position + aiTransform.forward * 4f + Vector3.up * 1.6f;
            navigationObstacle.transform.rotation = aiTransform.rotation;
            navigationObstacle.transform.localScale = new Vector3(20f, 3f, 0.5f);
            target.transform.position += aiTransform.right * 4f;
            Physics.SyncTransforms();
            Invoke(perception, "Tick", 108d);
            Invoke(decision, "Tick", 108d);
            Assert.AreEqual("Search", GetProperty<object>(decision, "State").ToString());

            Invoke(decision, "Tick", 110.1d);
            Assert.AreEqual("Return", GetProperty<object>(decision, "State").ToString());
            Invoke(decision, "Tick", 111.2d);
            Assert.AreEqual("Patrol", GetProperty<object>(decision, "State").ToString());

            object snapshot = Invoke(decision, "CreateDebugSnapshot");
            Array history = GetProperty<Array>(snapshot, "History");
            CollectionAssert.AreEqual(
                new[] { "Investigate", "Combat", "Search", "Return", "Patrol" },
                history.Cast<object>().Select(record => GetProperty<object>(record, "Current").ToString()).ToArray());
            Assert.Greater(GetProperty<Array>(snapshot, "Candidates").Length, 0);
            Assert.IsNotEmpty(GetProperty<string>(snapshot, "SelectionReason"));

            GameObject panelObject = new GameObject("AIDecisionDebugPanel");
            Component panel = panelObject.AddComponent(RequireRuntimeType("CGame.AIDecisionDebugPanel"));
            Invoke(panel, "Bind", decision);
            object panelSnapshot = GetProperty<object>(panel, "Snapshot");
            Assert.AreEqual("Patrol", GetProperty<object>(panelSnapshot, "State").ToString());
            Assert.AreEqual(history.Length, GetProperty<Array>(panelSnapshot, "History").Length);
            DestroyObject(panelObject);

            Invoke(perception, "PublishSound", "interrupt-sound", soundPosition, 112d, 4f);
            Invoke(perception, "Tick", 112d);
            Invoke(decision, "Tick", 112d);
            Assert.AreEqual("Investigate", GetProperty<object>(decision, "State").ToString());
            Invoke(decision, "NotifyPathFailure", 112.1d);
            Assert.AreEqual("Search", GetProperty<object>(decision, "State").ToString());
            Invoke(decision, "NotifyTargetDeath", 112.2d);
            Assert.AreEqual("Return", GetProperty<object>(decision, "State").ToString());

            Invoke(perception, "Tick", 117d);
            Invoke(decision, "Tick", 117d);
            Assert.AreEqual("Patrol", GetProperty<object>(decision, "State").ToString());
            object executionBeforeDeath = GetProperty<object>(decision, "CurrentExecution");
            object health = GetProperty<object>(runtime, "Health");
            object runtimeId = GetProperty<object>(runtime, "RuntimeId");
            object damageEvent = Activator.CreateInstance(
                RequireRuntimeType("CGame.DamageEvent"),
                "decision-kill",
                "test-source",
                runtimeId.GetType().GetProperty("Value").GetValue(runtimeId),
                500f,
                aiTransform.position,
                Vector3.back,
                117.1d);
            Assert.IsTrue((bool)Invoke(health, "ApplyDamage", damageEvent));
            Assert.IsFalse(GetProperty<bool>(runtime, "IsActive"));
            Assert.AreEqual("Cancelled", GetProperty<object>(executionBeforeDeath, "Status").ToString());
            Assert.DoesNotThrow(() => Invoke(decision, "Shutdown"));
            Assert.DoesNotThrow(() => Invoke(decision, "Shutdown"));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AISquadDeconfliction_SixFormalAIsShareFuzzyReportsAndReleaseQuotas()
        {
            BuildGroundNavigationData();
            yield return CharacterSpawnTestConfiguration
                .EnsureResourcesReady();
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithYooAssetDefinitions();
            var operations = new List<object>();
            var runtimes = new List<object>();
            for (int i = 0; i < 6; i++)
            {
                object operation = Invoke(
                    spawnManager,
                    "BeginSpawn",
                    CreateAIRequest($"squad-ai-{i}", $"SquadAI{i}", new Vector3(i - 2.5f, 0f, -4f)));
                operations.Add(operation);
            }

            yield return AdvanceSpawn(spawnManager, 30);
            for (int i = 0; i < operations.Count; i++)
            {
                Assert.AreEqual("CharacterReady", GetProperty<object>(operations[i], "State").ToString());
                runtimes.Add(GetAIRuntime(spawnManager, GetProperty<object>(operations[i], "RuntimeId")));
            }

            acceptanceCameraObject = new GameObject("AIDebugAcceptanceCamera");
            Camera acceptanceCamera = acceptanceCameraObject.AddComponent<Camera>();
            acceptanceCamera.clearFlags = CameraClearFlags.SolidColor;
            acceptanceCamera.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
            acceptanceCamera.fieldOfView = 52f;
            acceptanceCameraObject.transform.position = new Vector3(0f, 8f, -12f);
            acceptanceCameraObject.transform.LookAt(new Vector3(0f, 1.2f, -0.5f));
            acceptanceLightObject = new GameObject("AIDebugAcceptanceLight");
            Light acceptanceLight = acceptanceLightObject.AddComponent<Light>();
            acceptanceLight.type = LightType.Directional;
            acceptanceLight.intensity = 1.2f;
            acceptanceLightObject.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

            object sharedContext = GetProperty<object>(GetProperty<object>(runtimes[0], "SquadMember"), "Context");
            Assert.NotNull(sharedContext);
            for (int i = 1; i < runtimes.Count; i++)
            {
                Assert.AreSame(
                    sharedContext,
                    GetProperty<object>(GetProperty<object>(runtimes[i], "SquadMember"), "Context"));
            }

            for (int i = 0; i < runtimes.Count; i++)
            {
                Assert.NotNull(
                    GetProperty<object>(runtimes[i], "DebugRuntime"),
                    $"Formal AI {i} is missing the read-only debug runtime.");
            }

            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "AISquadReportTarget";
            ground.GetComponent<Renderer>().material.color = new Color(0.12f, 0.16f, 0.2f);
            target.GetComponent<Renderer>().material.color = new Color(0.82f, 0.18f, 0.12f);
            Transform observerTransform = GetProperty<Transform>(runtimes[0], "Transform");
            target.transform.position = observerTransform.position + observerTransform.forward * 8f + Vector3.up * 1.6f;
            Component perceptionTarget = target.AddComponent(RequireRuntimeType("CGame.AIPerceptionTargetBehaviour"));
            Invoke(perceptionTarget, "Configure", "squad-player", null);
            object observerPerception = GetProperty<object>(runtimes[0], "Perception");
            Assert.IsTrue((bool)Invoke(observerPerception, "RegisterTarget", perceptionTarget));
            Physics.SyncTransforms();
            Invoke(observerPerception, "Tick", 100d);
            Invoke(observerPerception, "Tick", 100.3d);
            object observerMember = GetProperty<object>(runtimes[0], "SquadMember");
            Invoke(observerMember, "Tick", 100.3d);

            object receiverPerception = GetProperty<object>(runtimes[1], "Perception");
            Assert.IsNull(FindMemoryRecord(receiverPerception, "Visual"));
            object[] suggestionArguments = { 100.9d, null };
            Assert.IsTrue((bool)InvokeWithArguments(
                GetProperty<object>(runtimes[1], "SquadMember"),
                "TryGetSuggestion",
                suggestionArguments));
            Assert.IsFalse(GetProperty<bool>(suggestionArguments[1], "CanAuthorizeFire"));
            Assert.Greater(GetProperty<float>(suggestionArguments[1], "UncertaintyRadius"), 0f);
            Assert.AreNotEqual(
                target.transform.position,
                GetProperty<Vector3>(suggestionArguments[1], "EstimatedPosition"));

            int shooters = 0;
            int repositioners = 0;
            int coverOwners = 0;
            object coverReservation = null;
            Type coverServiceType = RequireRuntimeType("CGame.CoverReservationService");
            object coverService = coverServiceType.GetProperty("Shared").GetValue(null);
            for (int i = 0; i < runtimes.Count; i++)
            {
                object member = GetProperty<object>(runtimes[i], "SquadMember");
                if ((bool)Invoke(member, "TryAcquireShooter", 101d, 2d))
                {
                    shooters++;
                }

                if ((bool)Invoke(member, "TryAcquireReposition", 101d, 2d))
                {
                    repositioners++;
                }

                object runtimeId = GetProperty<object>(runtimes[i], "RuntimeId");
                string ownerId = (string)runtimeId.GetType().GetProperty("Value").GetValue(runtimeId);
                object[] coverArguments = { "squad-shared-cover", ownerId, null };
                if ((bool)InvokeWithArguments(coverService, "TryReserve", coverArguments))
                {
                    coverOwners++;
                    coverReservation = coverArguments[2];
                }
            }

            Assert.AreEqual(1, shooters, "Only one AI may begin the shared firing window.");
            Assert.AreEqual(1, repositioners, "Only one AI may begin the shared reposition window.");
            Assert.AreEqual(1, coverOwners, "Only one AI may own the same CoverSlot.");
            Assert.IsTrue((bool)Invoke(coverReservation, "Release"));

            object debugRuntime = GetProperty<object>(runtimes[0], "DebugRuntime");
            object beforeDebug = Invoke(debugRuntime, "CreateDebugSnapshot");
            Assert.NotNull(GetProperty<object>(beforeDebug, "Perception"));
            Assert.NotNull(GetProperty<object>(beforeDebug, "Navigation"));
            Assert.NotNull(GetProperty<object>(beforeDebug, "Decision"));
            Assert.NotNull(GetProperty<object>(beforeDebug, "CoverCombat"));
            Assert.NotNull(GetProperty<object>(beforeDebug, "Squad"));
            int shotsBeforeDebug = GetProperty<int>(GetProperty<object>(GetProperty<object>(runtimes[0], "WeaponRuntime"), "Weapon"), "ShotsFired");
            Invoke(debugRuntime, "SetPanelVisible", true);
            object afterDebug = Invoke(debugRuntime, "CreateDebugSnapshot");
            Invoke(debugRuntime, "SetPanelVisible", false);
            Assert.AreEqual(
                GetProperty<object>(GetProperty<object>(beforeDebug, "Decision"), "State"),
                GetProperty<object>(GetProperty<object>(afterDebug, "Decision"), "State"));
            Assert.AreEqual(
                shotsBeforeDebug,
                GetProperty<int>(GetProperty<object>(GetProperty<object>(runtimes[0], "WeaponRuntime"), "Weapon"), "ShotsFired"));

            string evidenceDirectory = Path.Combine(Application.temporaryCachePath, "AIDebugAcceptance-008");
            Directory.CreateDirectory(evidenceDirectory);
            string beforePath = Path.Combine(evidenceDirectory, "before-six-ai-world-1920x1080.png");
            string afterPath = Path.Combine(evidenceDirectory, "after-six-ai-debug-1920x1080.png");
            string panelHiddenPath = Path.Combine(evidenceDirectory, "six-ai-panel-hidden-game-view.png");
            string panelVisiblePath = Path.Combine(evidenceDirectory, "six-ai-panel-visible-game-view.png");
            CaptureCamera(acceptanceCamera, beforePath, 1920, 1080);
            CreateAcceptanceDebugLabels(runtimes, acceptanceCamera);
            CaptureCamera(acceptanceCamera, afterPath, 1920, 1080);
            Screen.SetResolution(1920, 1080, false);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(panelHiddenPath);
            yield return new WaitForEndOfFrame();

            string[] markerNames =
            {
                "CGame.AI.Perception.Update",
                "CGame.AI.Decision.Update",
                "CGame.AI.Navigation.Update",
                "CGame.AI.CoverCombat.Update",
                "CGame.AI.Squad.Update"
            };
            var recorders = markerNames
                .Select(markerName => ProfilerRecorder.StartNew(ProfilerCategory.Scripts, markerName, 256))
                .ToArray();
            var sampledFrameTimes = new List<float>(150);

            Invoke(debugRuntime, "SetPanelVisible", true);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(panelVisiblePath);
            yield return new WaitForEndOfFrame();
            Invoke(debugRuntime, "SetPanelVisible", false);
            for (int frame = 0; frame < 180; frame++)
            {
                yield return null;
                if (frame >= 30)
                {
                    sampledFrameTimes.Add(Time.unscaledDeltaTime);
                }
            }
            float averageFrameTime = sampledFrameTimes.Average();
            float averageFramesPerSecond = 1f / averageFrameTime;
            float percentile95FrameTime = sampledFrameTimes
                .OrderBy(frameTime => frameTime)
                .ElementAt(Mathf.Clamp(Mathf.CeilToInt(sampledFrameTimes.Count * 0.95f) - 1, 0, sampledFrameTimes.Count - 1));
            TestContext.WriteLine(
                $"AIDebugAcceptance-008 six-AI sample: avg={averageFramesPerSecond:F1} FPS, " +
                $"p95={percentile95FrameTime * 1000f:F2} ms, frames={sampledFrameTimes.Count}, " +
                $"evidence={evidenceDirectory}");
            Assert.GreaterOrEqual(averageFramesPerSecond, 60f, "Six formal AIs did not sustain an average 60 FPS on this machine.");
            Assert.LessOrEqual(percentile95FrameTime, 1f / 30f, "Six-AI p95 frame time exceeded the 30 FPS guardrail.");

            for (int i = 0; i < recorders.Length; i++)
            {
                ProfilerRecorder recorder = recorders[i];
                TestContext.WriteLine(
                    $"{markerNames[i]}: valid={recorder.Valid}, samples={recorder.Count}, " +
                    $"last={recorder.LastValue} ns");
                Assert.IsTrue(recorder.Valid, $"Profiler marker was not registered: {markerNames[i]}");
                Assert.Greater(recorder.Count, 0, $"Profiler marker produced no samples: {markerNames[i]}");
                recorder.Dispose();
            }

            object observerHealth = GetProperty<object>(runtimes[0], "Health");
            object observerRuntimeId = GetProperty<object>(runtimes[0], "RuntimeId");
            object death = Activator.CreateInstance(
                RequireRuntimeType("CGame.DamageEvent"),
                "squad-observer-death",
                "test",
                observerRuntimeId.GetType().GetProperty("Value").GetValue(observerRuntimeId),
                500f,
                observerTransform.position,
                Vector3.back,
                102d);
            Assert.IsTrue((bool)Invoke(observerHealth, "ApplyDamage", death));
            Assert.IsFalse(GetProperty<bool>(runtimes[0], "IsActive"));
            Assert.Greater(GetProperty<int>(sharedContext, "ReportCount"), 0);

            Invoke(sharedContext, "Advance", 105d);
            Assert.AreEqual(0, GetProperty<int>(sharedContext, "ReportCount"));
            Assert.AreEqual(0, GetProperty<int>(sharedContext, "LeaseCount"));

            for (int i = 1; i < operations.Count; i++)
            {
                object reason = Enum.Parse(RequireRuntimeType("CGame.CharacterDespawnReason"), "Requested");
                Assert.IsTrue((bool)Invoke(
                    spawnManager,
                    "Despawn",
                    GetProperty<object>(operations[i], "RuntimeId"),
                    reason));
            }

            Invoke(spawnManager, "Update", 0f);
            Assert.DoesNotThrow(() => Invoke(spawnManager, "Shutdown"));
            Assert.IsTrue(GetProperty<bool>(sharedContext, "IsShutdown"));
            Assert.AreEqual(0, GetProperty<int>(sharedContext, "LeaseCount"));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static void CaptureCamera(Camera camera, string path, int width, int height)
        {
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            try
            {
                camera.targetTexture = renderTexture;
                camera.aspect = (float)width / height;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                DestroyObject(texture);
                DestroyObject(renderTexture);
            }
        }

        private void CreateAcceptanceDebugLabels(IReadOnlyList<object> runtimes, Camera camera)
        {
            for (int i = 0; i < runtimes.Count; i++)
            {
                object debugRuntime = GetProperty<object>(runtimes[i], "DebugRuntime");
                object snapshot = Invoke(debugRuntime, "CreateDebugSnapshot");
                object decision = GetProperty<object>(snapshot, "Decision");
                object state = GetProperty<object>(decision, "State");
                object currentAction = GetProperty<object>(decision, "CurrentAction");
                string stateText = state.ToString();
                string actionText = currentAction.ToString();
                Transform runtimeTransform = GetProperty<Transform>(runtimes[i], "Transform");
                var labelObject = new GameObject($"AIDebugAcceptanceLabel{i}");
                TextMesh label = labelObject.AddComponent<TextMesh>();
                label.text = $"AI{i}\n{stateText[0]}/{actionText[0]}";
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 64;
                label.characterSize = 0.035f;
                label.color = new Color(0.15f, 0.9f, 1f);
                labelObject.transform.position = runtimeTransform.position + Vector3.up * 2.8f;
                labelObject.transform.rotation = camera.transform.rotation;
                acceptanceDebugObjects.Add(labelObject);
            }
        }

        [UnityTest]
        public IEnumerator AICoverCombat_MovesAimsBurstsRepositionsAndHandlesRangeFailures()
        {
            BuildGroundNavigationData();
            yield return CharacterSpawnTestConfiguration
                .EnsureResourcesReady();
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithYooAssetDefinitions();
            object operation = Invoke(
                spawnManager,
                "BeginSpawn",
                CreateAIRequest("cover-combat-ai", "CoverCombatAI", new Vector3(0f, 0f, -4f)));
            yield return AdvanceSpawn(spawnManager, 6);
            Assert.AreEqual("CharacterReady", GetProperty<object>(operation, "State").ToString());

            object runtime = GetAIRuntime(spawnManager, GetProperty<object>(operation, "RuntimeId"));
            object perception = GetProperty<object>(runtime, "Perception");
            object decision = GetProperty<object>(runtime, "Decision");
            object coverCombat = GetProperty<object>(runtime, "CoverCombat");
            object navigation = GetProperty<object>(runtime, "Navigation");
            Assert.NotNull(coverCombat, "Cover Combat was not attached to the formal AI registration.");
            Transform aiTransform = GetProperty<Transform>(runtime, "Transform");

            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "AICoverCombatTarget";
            target.transform.position = new Vector3(0f, 1.6f, 4f);
            target.transform.localScale = new Vector3(1.5f, 2f, 1f);
            Component targetPerception = target.AddComponent(RequireRuntimeType("CGame.AIPerceptionTargetBehaviour"));
            Invoke(targetPerception, "Configure", "cover-target", null);
            Component targetHealth = target.AddComponent(RequireRuntimeType("CGame.HealthComponent"));
            Invoke(targetHealth, "Configure", "cover-target", 200f);
            Assert.IsTrue((bool)Invoke(perception, "RegisterTarget", targetPerception));
            Physics.SyncTransforms();
            Invoke(perception, "Tick", 100d);
            Invoke(perception, "Tick", 100.3d);
            Invoke(decision, "Tick", 100.3d);
            Assert.AreEqual("Combat", GetProperty<object>(decision, "State").ToString());

            navigationObstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            navigationObstacle.name = "AICoverCombatWall";
            navigationObstacle.transform.position = new Vector3(0f, 1f, 0f);
            navigationObstacle.transform.localScale = new Vector3(4f, 2f, 0.5f);
            GameObject slotObject = new GameObject("StandingCoverSlot");
            slotObject.transform.position = new Vector3(0f, 0f, -1f);
            Component slot = slotObject.AddComponent(RequireRuntimeType("CGame.CoverSlotBehaviour"));
            Invoke(
                slot,
                "Configure",
                "standing-slot",
                Enum.Parse(RequireRuntimeType("CGame.CoverStance"), "Standing"),
                new Vector3(3f, 1.4f, 0f),
                0.15f);
            Physics.SyncTransforms();

            ((Behaviour)decision).enabled = false;
            ((Behaviour)coverCombat).enabled = false;
            Invoke(coverCombat, "Tick", 100.3d);
            object selectionSnapshot = Invoke(coverCombat, "CreateDebugSnapshot");
            Array selectionCandidates = GetProperty<Array>(selectionSnapshot, "Candidates");
            string selectionDetails = string.Join(
                " | ",
                selectionCandidates.Cast<object>().Select(candidate =>
                    $"{GetProperty<string>(candidate, "SlotId")}:{GetProperty<float>(candidate, "Score")}:" +
                    string.Join(",", GetProperty<string[]>(candidate, "Reasons"))));
            Assert.AreEqual(
                "MoveToCover",
                GetProperty<object>(coverCombat, "Action").ToString(),
                selectionDetails);
            object reservation = GetProperty<object>(coverCombat, "Reservation");
            Assert.NotNull(reservation);
            Assert.AreEqual("standing-slot", GetProperty<string>(reservation, "SlotId"));

            bool arrived = false;
            for (int i = 0; i < 220; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                if (GetProperty<object>(GetProperty<object>(navigation, "LastOutput"), "State").ToString() == "Arrived")
                {
                    arrived = true;
                    break;
                }
            }

            Assert.IsTrue(arrived, $"AI did not reach cover. Position: {aiTransform.position}");
            Invoke(coverCombat, "Tick", 101d);
            Assert.AreEqual("Aim", GetProperty<object>(coverCombat, "Action").ToString());

            navigationObstacle.GetComponent<Collider>().enabled = false;
            Physics.SyncTransforms();
            Invoke(coverCombat, "Tick", 102d);
            Assert.AreEqual("FireBurst", GetProperty<object>(coverCombat, "Action").ToString());
            object controller = GetProperty<object>(runtime, "Controller");
            object weaponRuntime = GetProperty<object>(runtime, "WeaponRuntime");
            object weapon = GetProperty<object>(weaponRuntime, "Weapon");
            int shotsBefore = GetProperty<int>(weapon, "ShotsFired");
            for (int i = 0; i < 5; i++)
            {
                Invoke(controller, "UpdatingController", 0.11f);
                Invoke(weaponRuntime, "Advance", 0.11f);
                Invoke(coverCombat, "Tick", 102.1d + i * 0.11d);
            }

            Assert.GreaterOrEqual(GetProperty<int>(weapon, "ShotsFired") - shotsBefore, 3);
            Assert.AreEqual("Pause", GetProperty<object>(coverCombat, "Action").ToString());
            object lastShot = GetProperty<object>(weapon, "LastShot");
            Assert.Greater(
                Vector3.Dot(GetProperty<Vector3>(lastShot, "Direction"), GetProperty<Transform>(runtime, "Muzzle").forward),
                0.999f);

            Invoke(coverCombat, "RequestReposition", 103d, "test-reposition");
            Assert.AreEqual("Reposition", GetProperty<object>(coverCombat, "Action").ToString());
            Assert.IsFalse(GetProperty<bool>(reservation, "IsActive"));
            slotObject.SetActive(false);

            target.transform.position = new Vector3(0f, 1.6f, 12f);
            Physics.SyncTransforms();
            Invoke(perception, "Tick", 103d);
            Invoke(perception, "Tick", 103.3d);
            Invoke(coverCombat, "Tick", 103d);
            Assert.AreEqual(
                "Approach",
                GetProperty<object>(coverCombat, "Action").ToString(),
                $"Threat: {GetProperty<Vector3>(coverCombat, "ThreatPosition")}, AI: {aiTransform.position}");

            Invoke(coverCombat, "RequestReposition", 104d, "close-target");
            target.transform.position = aiTransform.position + aiTransform.forward + Vector3.up * 1.6f;
            Physics.SyncTransforms();
            Invoke(perception, "Tick", 104d);
            Invoke(perception, "Tick", 104.3d);
            Invoke(coverCombat, "Tick", 104d);
            Assert.AreEqual("Retreat", GetProperty<object>(coverCombat, "Action").ToString());

            Invoke(coverCombat, "NotifyPathFailure", 104.1d);
            Assert.AreEqual("Reposition", GetProperty<object>(coverCombat, "Action").ToString());
            object snapshot = Invoke(coverCombat, "CreateDebugSnapshot");
            Assert.IsNotEmpty(GetProperty<string>(snapshot, "Reason"));

            object health = GetProperty<object>(runtime, "Health");
            object runtimeId = GetProperty<object>(runtime, "RuntimeId");
            object lethalDamage = Activator.CreateInstance(
                RequireRuntimeType("CGame.DamageEvent"),
                "cover-combat-death",
                "test",
                runtimeId.GetType().GetProperty("Value").GetValue(runtimeId),
                500f,
                aiTransform.position,
                Vector3.back,
                105d);
            Assert.IsTrue((bool)Invoke(health, "ApplyDamage", lethalDamage));
            Assert.IsFalse(GetProperty<bool>(runtime, "IsActive"));
            Assert.AreEqual("Idle", GetProperty<object>(coverCombat, "Action").ToString());
            Assert.DoesNotThrow(() => Invoke(coverCombat, "Shutdown"));
            Assert.DoesNotThrow(() => Invoke(coverCombat, "Shutdown"));
            DestroyObject(slotObject);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AINavigation_FollowsCornersAroundObstacleAndStopsForUnreachableTarget()
        {
            BuildNavigationData();
            navigationObstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            navigationObstacle.name = "AINavigationObstacle";
            navigationObstacle.transform.position = new Vector3(0f, 1f, 0f);
            navigationObstacle.transform.localScale = new Vector3(2f, 2f, 8f);
            Physics.SyncTransforms();

            yield return CharacterSpawnTestConfiguration
                .EnsureResourcesReady();
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithYooAssetDefinitions();
            object operation = Invoke(spawnManager, "BeginSpawn", CreateAIRequest("navigation-ai", "NavigationAI", new Vector3(-4f, 0f, -4f)));
            yield return AdvanceSpawn(spawnManager, 6);
            Assert.AreEqual("CharacterReady", GetProperty<object>(operation, "State").ToString());

            object runtimeId = GetProperty<object>(operation, "RuntimeId");
            object runtime = GetAIRuntime(spawnManager, runtimeId);
            object navigation = GetProperty<object>(runtime, "Navigation");
            Assert.NotNull(navigation);
            object path = Invoke(navigation, "SetDestination", new Vector3(4f, 0f, 4f));
            Assert.AreEqual("Complete", GetProperty<object>(path, "Status").ToString());
            Assert.GreaterOrEqual(GetProperty<Vector3[]>(path, "Corners").Length, 3);

            Transform characterTransform = GetProperty<Transform>(runtime, "Transform");
            bool arrived = false;
            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                object output = GetProperty<object>(navigation, "LastOutput");
                if (GetProperty<object>(output, "State").ToString() == "Arrived")
                {
                    arrived = true;
                    break;
                }
            }

            Assert.IsTrue(arrived, $"AI did not arrive. Position: {characterTransform.position}");
            Assert.Less(Vector3.Distance(
                new Vector3(characterTransform.position.x, 0f, characterTransform.position.z),
                new Vector3(4f, 0f, 4f)), 0.75f);

            object unreachable = Invoke(navigation, "SetDestination", new Vector3(30f, 0f, 30f));
            Assert.AreEqual("DestinationOutsideNavMesh", GetProperty<object>(unreachable, "Status").ToString());
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }

            Vector3 settledPosition = characterTransform.position;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }

            Assert.Less(Vector3.Distance(settledPosition, characterTransform.position), 0.05f);
            Assert.DoesNotThrow(() => Invoke(navigation, "Cancel"));
            Assert.DoesNotThrow(() => Invoke(navigation, "Cancel"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AISpawn_ControlCombatDeathDespawnRespawnAndShutdown_StayOwned()
        {
            yield return CharacterSpawnTestConfiguration
                .EnsureResourcesReady();
            object spawnManager = CharacterSpawnTestConfiguration.CreateManagerWithYooAssetDefinitions();
            object localOperation = Invoke(spawnManager, "BeginSpawn", CreateLocalPlayerRequest("local-control", "LocalControlCharacter", new Vector3(-4f, 0f, 0f)));
            yield return AdvanceSpawn(spawnManager, 6);
            Assert.AreEqual("CharacterReady", GetProperty<object>(localOperation, "State").ToString());
            Assert.IsFalse(TryGetAIRuntime(spawnManager, GetProperty<object>(localOperation, "RuntimeId"), out _));

            object firstOperation = Invoke(spawnManager, "BeginSpawn", CreateAIRequest("ai-first", "FirstAI", Vector3.zero));
            yield return AdvanceSpawn(spawnManager, 6);

            Assert.AreEqual("CharacterReady", GetProperty<object>(firstOperation, "State").ToString());
            object firstRuntimeId = GetProperty<object>(firstOperation, "RuntimeId");
            object firstRuntime = GetAIRuntime(spawnManager, firstRuntimeId);
            Assert.AreSame(firstOperation, Invoke(spawnManager, "BeginSpawn", CreateAIRequest("ai-first", "DuplicateAI", Vector3.one)));
            Assert.AreSame(firstRuntime, GetAIRuntime(spawnManager, firstRuntimeId));
            Assert.IsTrue(GetProperty<bool>(firstRuntime, "IsActive"));
            Assert.AreEqual("AIController", GetProperty<object>(firstRuntime, "Controller").GetType().Name);

            Transform rightGrip = GetProperty<Transform>(firstRuntime, "RightHandGrip");
            Transform leftSupport = GetProperty<Transform>(firstRuntime, "LeftHandSupport");
            Transform muzzle = GetProperty<Transform>(firstRuntime, "Muzzle");
            Assert.AreEqual("RightHandGrip", rightGrip.name);
            Assert.AreEqual("LeftHandSupport", leftSupport.name);
            Assert.AreEqual("Muzzle", muzzle.name);
            Assert.AreEqual("PrototypeRifle", rightGrip.parent.name);
            Assert.NotNull(rightGrip.parent.parent);

            GameObject character = GameObject.Find("FirstAI");
            Assert.NotNull(character);
            Vector3 startPosition = character.transform.position;
            SubmitFrame(firstRuntime, Vector3.forward, Vector3.forward, false, false, false);
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }

            Assert.Greater(character.transform.position.z, startPosition.z + 0.05f);

            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "AICombatTarget";
            target.transform.position = character.transform.position + new Vector3(0f, 1.1f, 8f);
            target.transform.localScale = new Vector3(2f, 2.5f, 1f);
            Component targetHealth = target.AddComponent(RequireRuntimeType("CGame.HealthComponent"));
            Invoke(targetHealth, "Configure", "ai-target", 40f);
            Physics.SyncTransforms();

            SubmitFrame(firstRuntime, Vector3.zero, Vector3.forward, false, true, false);
            object controller = GetProperty<object>(firstRuntime, "Controller");
            Invoke(controller, "UpdatingController", 0.02f);
            object weaponRuntime = GetProperty<object>(firstRuntime, "WeaponRuntime");
            Invoke(weaponRuntime, "Advance", 0f);

            Assert.Greater(Vector3.Dot(muzzle.forward, Vector3.forward), 0.999f);
            Assert.IsFalse(GetProperty<bool>(targetHealth, "IsAlive"));

            SubmitFrame(firstRuntime, Vector3.zero, Vector3.forward, false, false, true);
            Invoke(controller, "UpdatingController", 0.02f);
            Invoke(weaponRuntime, "Advance", 0f);
            object weapon = GetProperty<object>(weaponRuntime, "Weapon");
            Assert.IsTrue(GetProperty<bool>(weapon, "IsReloading"));
            Invoke(weaponRuntime, "Advance", 0.6f);
            Assert.IsFalse(GetProperty<bool>(weapon, "IsReloading"));
            Assert.AreEqual(12, GetProperty<int>(weapon, "AmmoInMagazine"));

            object aiHealth = GetProperty<object>(firstRuntime, "Health");
            object damageEvent = Activator.CreateInstance(
                RequireRuntimeType("CGame.DamageEvent"),
                "kill-ai",
                "test-source",
                GetProperty<object>(firstRuntime, "RuntimeId").GetType().GetProperty("Value").GetValue(firstRuntimeId),
                500f,
                character.transform.position,
                Vector3.back,
                1d);
            Assert.IsTrue((bool)Invoke(aiHealth, "ApplyDamage", damageEvent));
            Assert.IsFalse(GetProperty<bool>(firstRuntime, "IsActive"));
            Assert.IsFalse(GetProperty<bool>(firstRuntime, "IsAlive"));

            object reason = Enum.Parse(RequireRuntimeType("CGame.CharacterDespawnReason"), "Requested");
            Assert.IsTrue((bool)Invoke(spawnManager, "Despawn", firstRuntimeId, reason));
            Invoke(spawnManager, "Update", 0f);
            yield return null;
            Assert.IsFalse(TryGetAIRuntime(spawnManager, firstRuntimeId, out _));
            Assert.IsNull(GameObject.Find("FirstAI"));

            object savedBinder = GetField(spawnManager, "aiControllerBinder");
            SetField(spawnManager, "aiControllerBinder", null);
            object failedOperation = Invoke(spawnManager, "BeginSpawn", CreateAIRequest("ai-bind-failure", "FailedAI", new Vector3(4f, 0f, 0f)));
            yield return AdvanceSpawn(spawnManager, 6);
            Assert.AreEqual("Failed", GetProperty<object>(failedOperation, "State").ToString());
            Assert.IsNull(GameObject.Find("FailedAI"));
            SetField(spawnManager, "aiControllerBinder", savedBinder);

            object secondOperation = Invoke(spawnManager, "BeginSpawn", CreateAIRequest("ai-second", "SecondAI", new Vector3(2f, 0f, 0f)));
            yield return AdvanceSpawn(spawnManager, 6);
            Assert.AreEqual("CharacterReady", GetProperty<object>(secondOperation, "State").ToString());
            object secondRuntimeId = GetProperty<object>(secondOperation, "RuntimeId");
            Assert.AreNotEqual(firstRuntimeId, secondRuntimeId);
            Assert.IsTrue(TryGetAIRuntime(spawnManager, secondRuntimeId, out object secondRuntime));
            Assert.IsTrue(GetProperty<bool>(secondRuntime, "IsActive"));

            Assert.DoesNotThrow(() => Invoke(spawnManager, "Shutdown"));
            Assert.DoesNotThrow(() => Invoke(spawnManager, "Shutdown"));
            Assert.IsFalse(TryGetAIRuntime(spawnManager, secondRuntimeId, out _));
            LogAssert.NoUnexpectedReceived();
        }

        private static object CreateAIRequest(string requestIdValue, string displayName, Vector3 position)
        {
            CharacterDefinition definition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            Type requestType = RequireRuntimeType("CGame.CharacterSpawnRequest");
            object requestId = Activator.CreateInstance(RequireRuntimeType("CGame.CharacterSpawnRequestId"), requestIdValue);
            object placement = Activator.CreateInstance(RequireRuntimeType("CGame.CharacterSpawnPlacement"), position, Quaternion.identity);
            return Activator.CreateInstance(
                requestType,
                requestId,
                definition.DefinitionId,
                CharacterControlKind.AI,
                placement,
                InputType.Player,
                displayName);
        }

        private static object CreateLocalPlayerRequest(string requestIdValue, string displayName, Vector3 position)
        {
            CharacterDefinition definition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            Type requestType = RequireRuntimeType("CGame.CharacterSpawnRequest");
            object requestId = Activator.CreateInstance(RequireRuntimeType("CGame.CharacterSpawnRequestId"), requestIdValue);
            object placement = Activator.CreateInstance(RequireRuntimeType("CGame.CharacterSpawnPlacement"), position, Quaternion.identity);
            return Activator.CreateInstance(
                requestType,
                requestId,
                definition.DefinitionId,
                CharacterControlKind.LocalPlayer,
                placement,
                InputType.Player,
                displayName);
        }

        private void BuildNavigationData()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>
            {
                CreateBoxSource(new Vector3(0f, -0.1f, 0f), new Vector3(12f, 0.2f, 12f), 0),
                CreateBoxSource(new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 8f), 1),
            };
            navigationData = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                new Bounds(Vector3.zero, new Vector3(14f, 6f, 14f)),
                Vector3.zero,
                Quaternion.identity);
            Assert.NotNull(navigationData);
            navigationDataInstance = NavMesh.AddNavMeshData(navigationData);
        }

        private void BuildGroundNavigationData()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>
            {
                CreateBoxSource(new Vector3(0f, -0.1f, 0f), new Vector3(30f, 0.2f, 30f), 0),
            };
            navigationData = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                new Bounds(Vector3.zero, new Vector3(32f, 6f, 32f)),
                Vector3.zero,
                Quaternion.identity);
            Assert.NotNull(navigationData);
            navigationDataInstance = NavMesh.AddNavMeshData(navigationData);
        }

        private static NavMeshBuildSource CreateBoxSource(Vector3 position, Vector3 size, int area)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one),
                size = size,
                area = area,
            };
        }

        private static void SubmitFrame(
            object runtime,
            Vector3 movement,
            Vector3 aim,
            bool jump,
            bool fire,
            bool reload)
        {
            object frame = Activator.CreateInstance(
                RequireRuntimeType("CGame.AIControlFrame"),
                movement,
                aim,
                jump,
                fire,
                reload);
            Assert.IsTrue((bool)Invoke(runtime, "SubmitControlFrame", frame));
        }

        private static object FindMemoryRecord(object perception, string channelName)
        {
            object snapshot = Invoke(perception, "CreateDebugSnapshot");
            Array records = GetProperty<Array>(snapshot, "Records");
            foreach (object record in records)
            {
                if (GetProperty<object>(record, "Channel").ToString() == channelName)
                {
                    return record;
                }
            }

            return null;
        }

        private static object GetAIRuntime(object spawnManager, object runtimeId)
        {
            Assert.IsTrue(TryGetAIRuntime(spawnManager, runtimeId, out object runtime));
            return runtime;
        }

        private static bool TryGetAIRuntime(object spawnManager, object runtimeId, out object runtime)
        {
            object[] arguments = { runtimeId, null };
            bool found = (bool)InvokeWithArguments(spawnManager, "TryGetAIRuntime", arguments);
            runtime = arguments[1];
            return found;
        }

        private static IEnumerator AdvanceSpawn(
            object spawnManager,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                Invoke(spawnManager, "Update", 0f);
                yield return null;
            }
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
            return method.Invoke(target, arguments);
        }

        private static object InvokeWithArguments(object target, string methodName, object[] arguments)
        {
            return target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Runtime type was not found: {fullName}");
            return type;
        }

        private static void DestroyIfPresent(string objectName)
        {
            DestroyObject(GameObject.Find(objectName));
        }

        private static void DestroyObject(UnityEngine.Object targetObject)
        {
            if (targetObject != null)
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }
    }
}
