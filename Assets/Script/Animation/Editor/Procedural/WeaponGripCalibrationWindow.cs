using System;
using System.IO;
using System.Linq;
using CGame;
using CGame.InventoryEquipment;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor
{
    public sealed class WeaponGripCalibrationWindow : EditorWindow
    {
        private const float KnifeSourceYOffset = 0.03f;
        private static readonly Vector3 Ak12SourcePosition = new Vector3(-0.015f, -0.065f, 0.04f);
        private static readonly Quaternion Ak12SourceRotation =
            new Quaternion(0.14350314f, 0.6599624f, 0.46920574f, -0.5689484f);
        private static readonly Vector3 Ak12ViewSourcePosition = new Vector3(0f, 0.04f, 0f);
        private static readonly Quaternion Ak12ViewSourceRotation =
            new Quaternion(0.00007449071f, -0.005846337f, 0.0127402f, -0.9999017f);

        private WeaponDefinition definition;
        private PoseOffsetLayerSettings poseOffset;
        private AttachHandLayerSettings attachHand;
        private ViewLayerSettings viewLayer;
        private Vector3 knifeProjectDelta;
        private Vector3 ak12PositionDelta;
        private Vector3 ak12RotationDeltaEuler;
        private Vector3 ak12ViewPositionDelta;
        private Vector3 ak12ViewRotationDeltaEuler;

        [MenuItem("CGame/Animation/Weapon Grip Calibration")]
        public static void Open()
        {
            GetWindow<WeaponGripCalibrationWindow>("Weapon Grip Calibration");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneGuides;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGuides;
        }

        private void OnGUI()
        {
            definition = (WeaponDefinition)EditorGUILayout.ObjectField(
                "Weapon Definition", definition, typeof(WeaponDefinition), false);
            poseOffset = (PoseOffsetLayerSettings)EditorGUILayout.ObjectField(
                "Knife Pose Offset", poseOffset, typeof(PoseOffsetLayerSettings), false);
            attachHand = (AttachHandLayerSettings)EditorGUILayout.ObjectField(
                "AK12 Attach Hand", attachHand, typeof(AttachHandLayerSettings), false);
            viewLayer = (ViewLayerSettings)EditorGUILayout.ObjectField(
                "AK12 View Layer", viewLayer, typeof(ViewLayerSettings), false);

            DrawProfileOrder();
            DrawKnifeAuthoring();
            DrawAk12Authoring();
            DrawAk12ViewAuthoring();

            if (GUILayout.Button("Generate Calibration Report")) GenerateReport();
            DrawRuntimeMeasurement();
        }

        private void DrawProfileOrder()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Profile Order", EditorStyles.boldLabel);
            if (definition?.ArmedProfile == null)
            {
                EditorGUILayout.LabelField("None");
                return;
            }

            EditorGUILayout.LabelField(string.Join(" -> ",
                definition.ArmedProfile.Layers.Select(layer => layer == null ? "Missing" : layer.GetType().Name)));
        }

        private void DrawKnifeAuthoring()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Knife Pose", EditorStyles.boldLabel);
            knifeProjectDelta = EditorGUILayout.Vector3Field("Project Delta", knifeProjectDelta);
            EditorGUILayout.LabelField("Source", "IK WeaponBone / Component Add / (0, 0.03, 0)");
            EditorGUILayout.LabelField("Final", (new Vector3(0f, KnifeSourceYOffset, 0f) + knifeProjectDelta).ToString("F5"));
            using (new EditorGUI.DisabledScope(poseOffset == null))
            {
                if (GUILayout.Button("Write Knife Final To Settings")) WriteKnifePose(false);
                if (GUILayout.Button("Restore Knife Source Value")) WriteKnifePose(true);
            }
        }

        private void DrawAk12Authoring()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AK12 Attach Hand", EditorStyles.boldLabel);
            ak12PositionDelta = EditorGUILayout.Vector3Field("Position Delta", ak12PositionDelta);
            ak12RotationDeltaEuler = EditorGUILayout.Vector3Field("Rotation Delta", ak12RotationDeltaEuler);
            KTransform final = GetAk12Final();
            EditorGUILayout.LabelField("Source Position", Ak12SourcePosition.ToString("F6"));
            EditorGUILayout.LabelField("Final Position", final.Position.ToString("F6"));
            EditorGUILayout.LabelField("Final Rotation", Format(final.Rotation));
            using (new EditorGUI.DisabledScope(attachHand == null))
            {
                if (GUILayout.Button("Read AK12 Delta From Settings")) ReadAk12Delta();
                if (GUILayout.Button("Write AK12 Final To Settings")) WriteAk12Pose(false);
                if (GUILayout.Button("Restore AK12 Source Value")) WriteAk12Pose(true);
            }
        }

        private void DrawAk12ViewAuthoring()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AK12 First-Person View", EditorStyles.boldLabel);
            ak12ViewPositionDelta = EditorGUILayout.Vector3Field("Position Delta", ak12ViewPositionDelta);
            ak12ViewRotationDeltaEuler = EditorGUILayout.Vector3Field("Rotation Delta", ak12ViewRotationDeltaEuler);
            KTransform final = GetAk12ViewFinal();
            EditorGUILayout.LabelField("Source Position", Ak12ViewSourcePosition.ToString("F6"));
            EditorGUILayout.LabelField("Final Position", final.Position.ToString("F6"));
            EditorGUILayout.LabelField("Final Rotation", Format(final.Rotation));
            using (new EditorGUI.DisabledScope(viewLayer == null))
            {
                if (GUILayout.Button("Read View Delta From Settings")) ReadAk12ViewDelta();
                if (GUILayout.Button("Write View Final To Settings")) WriteAk12View(false);
                if (GUILayout.Button("Restore View Source Value")) WriteAk12View(true);
            }
        }

        private void WriteKnifePose(bool restoreSource)
        {
            Vector3 final = new Vector3(0f, KnifeSourceYOffset, 0f)
                + (restoreSource ? Vector3.zero : knifeProjectDelta);
            SerializedObject serialized = new SerializedObject(poseOffset);
            SerializedProperty offsets = serialized.FindProperty("poseOffsets");
            if (offsets.arraySize != 1)
            {
                throw new InvalidOperationException("Knife PoseOffset must contain exactly one entry.");
            }

            SerializedProperty pose = offsets.GetArrayElementAtIndex(0).FindPropertyRelative("Pose");
            pose.FindPropertyRelative("Pose").FindPropertyRelative("Position").vector3Value = final;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(poseOffset);
            AssetDatabase.SaveAssets();
        }

        private void ReadAk12Delta()
        {
            KTransform current = attachHand.HandPoseOffset;
            ak12PositionDelta = current.Position - Ak12SourcePosition;
            ak12RotationDeltaEuler = (Quaternion.Inverse(Ak12SourceRotation) * current.Rotation).eulerAngles;
        }

        private void WriteAk12Pose(bool restoreSource)
        {
            KTransform final = restoreSource
                ? new KTransform(Ak12SourcePosition, Ak12SourceRotation, Vector3.one)
                : GetAk12Final();
            SerializedObject serialized = new SerializedObject(attachHand);
            SerializedProperty pose = serialized.FindProperty("handPoseOffset");
            pose.FindPropertyRelative("Position").vector3Value = final.Position;
            pose.FindPropertyRelative("Rotation").quaternionValue = final.Rotation;
            pose.FindPropertyRelative("Scale").vector3Value = Vector3.one;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(attachHand);
            AssetDatabase.SaveAssets();
        }

        private KTransform GetAk12Final()
        {
            return new KTransform(
                Ak12SourcePosition + ak12PositionDelta,
                Quaternion.Normalize(Ak12SourceRotation * Quaternion.Euler(ak12RotationDeltaEuler)),
                Vector3.one);
        }

        private void ReadAk12ViewDelta()
        {
            KTransform current = viewLayer.IkWeaponBone.Pose;
            ak12ViewPositionDelta = current.Position - Ak12ViewSourcePosition;
            ak12ViewRotationDeltaEuler = (current.Rotation * Quaternion.Inverse(Ak12ViewSourceRotation)).eulerAngles;
        }

        private void WriteAk12View(bool restoreSource)
        {
            KTransform final = restoreSource
                ? new KTransform(Ak12ViewSourcePosition, Ak12ViewSourceRotation, Vector3.one)
                : GetAk12ViewFinal();
            SerializedObject serialized = new SerializedObject(viewLayer);
            SerializedProperty weaponPose = serialized.FindProperty("ikWeaponBone").FindPropertyRelative("Pose");
            weaponPose.FindPropertyRelative("Position").vector3Value = final.Position;
            weaponPose.FindPropertyRelative("Rotation").quaternionValue = final.Rotation;
            weaponPose.FindPropertyRelative("Scale").vector3Value = Vector3.one;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(viewLayer);
            AssetDatabase.SaveAssets();
        }

        private KTransform GetAk12ViewFinal()
        {
            return new KTransform(
                Ak12ViewSourcePosition + ak12ViewPositionDelta,
                Quaternion.Normalize(Quaternion.Euler(ak12ViewRotationDeltaEuler) * Ak12ViewSourceRotation),
                Vector3.one);
        }

        private void GenerateReport()
        {
            string path = EditorUtility.SaveFilePanel(
                "Save Weapon Grip Calibration Report",
                Application.dataPath,
                "weapon-grip-calibration.md",
                "md");
            if (string.IsNullOrEmpty(path)) return;

            KTransform final = attachHand == null ? GetAk12Final() : attachHand.HandPoseOffset;
            KTransform viewFinal = viewLayer == null ? GetAk12ViewFinal() : viewLayer.IkWeaponBone.Pose;
            string profileOrder = definition?.ArmedProfile == null
                ? "None"
                : string.Join(" -> ", definition.ArmedProfile.Layers.Select(layer => layer.GetType().Name));
            string runtime = TryGetRuntime(out RuntimeMeasurement measurement)
                ? $"- Right palm/grip: {measurement.RightPositionError:F6} m / {measurement.RightRotationError:F3} deg\n"
                    + $"- Left palm/grip: {measurement.LeftPositionError:F6} m / {measurement.LeftRotationError:F3} deg\n"
                    + $"- Right IK/grip: {measurement.RightIkPositionError:F6} m / {measurement.RightIkRotationError:F3} deg\n"
                    + $"- Left IK/grip: {measurement.LeftIkPositionError:F6} m / {measurement.LeftIkRotationError:F3} deg\n"
                : "- Runtime measurement: unavailable\n";
            File.WriteAllText(path,
                "# Weapon Grip Calibration\n\n"
                + $"- Definition: {definition?.name ?? "None"}\n"
                + $"- Profile: {profileOrder}\n"
                + $"- AK12 source position: {Ak12SourcePosition:F6}\n"
                + $"- AK12 final position: {final.Position:F6}\n"
                + $"- AK12 final rotation: {Format(final.Rotation)}\n"
                + $"- AK12 position delta: {final.Position - Ak12SourcePosition:F6}\n"
                + $"- View source position: {Ak12ViewSourcePosition:F6}\n"
                + $"- View final position: {viewFinal.Position:F6}\n"
                + $"- View final rotation: {Format(viewFinal.Rotation)}\n"
                + runtime);
            AssetDatabase.Refresh();
        }

        private void DrawRuntimeMeasurement()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Palm / Grip", EditorStyles.boldLabel);
            if (!TryGetRuntime(out RuntimeMeasurement measurement))
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode with the selected calibrated weapon armed to inspect both palms and IK targets.",
                    MessageType.Info);
                return;
            }

            DrawError("Right palm", measurement.RightPositionError, measurement.RightRotationError);
            DrawError("Left palm", measurement.LeftPositionError, measurement.LeftRotationError);
            DrawError("Right IK", measurement.RightIkPositionError, measurement.RightIkRotationError);
            DrawError("Left IK", measurement.LeftIkPositionError, measurement.LeftIkRotationError);
        }

        private static void DrawError(string label, float position, float rotation)
        {
            EditorGUILayout.LabelField(label, $"{position:F5} m / {rotation:F3} deg");
        }

        private void DrawSceneGuides(SceneView sceneView)
        {
            if (!TryGetRuntime(out RuntimeMeasurement measurement)) return;
            DrawPair(measurement.RightPalm, measurement.RightGrip, Color.cyan);
            DrawPair(measurement.LeftPalm, measurement.LeftGrip, Color.yellow);
            sceneView.Repaint();
        }

        private static void DrawPair(KTransform palm, Transform grip, Color color)
        {
            Handles.color = color;
            Handles.DrawLine(palm.Position, grip.position, 3f);
            float size = HandleUtility.GetHandleSize(grip.position) * 0.08f;
            Handles.ArrowHandleCap(0, grip.position, grip.rotation, size, EventType.Repaint);
            Handles.ArrowHandleCap(0, palm.Position, palm.Rotation, size, EventType.Repaint);
        }

        private bool TryGetRuntime(out RuntimeMeasurement measurement)
        {
            measurement = default;
            if (!EditorApplication.isPlaying || definition == null || definition.HandPalmCalibration == null) return false;
            GameInstance game = UnityEngine.Object.FindObjectOfType<GameInstance>();
            PlayerController controller = game?.RuntimeWorld?.GameMode?.PlayerController as PlayerController;
            Pawn pawn = controller?.ControlledPawn;
            WeaponInstance weapon = pawn?.GetComponent<EquipmentManagerComponent>()?.CurrentWeapon;
            if (weapon == null || weapon.Definition != definition) return false;

            HandPalmCalibration calibration = definition.HandPalmCalibration;
            Transform rightHand = FindUnique(pawn.Root.transform, calibration.RightHand.Name);
            Transform leftHand = FindUnique(pawn.Root.transform, calibration.LeftHand.Name);
            Transform rightIk = FindUnique(pawn.Root.transform, "IK RightHand");
            Transform leftIk = FindUnique(pawn.Root.transform, "IK LeftHand");
            Transform rightGrip = FindUnique(weapon.PresentationRoot.transform, "RightHandGrip");
            Transform leftGrip = FindUnique(weapon.PresentationRoot.transform, "LeftHandGrip");
            KTransform rightPalm = calibration.GetRightPalmWorld(rightHand);
            KTransform leftPalm = calibration.GetLeftPalmWorld(leftHand);
            measurement = new RuntimeMeasurement(
                rightPalm, leftPalm, rightGrip, leftGrip,
                Vector3.Distance(rightPalm.Position, rightGrip.position),
                Quaternion.Angle(rightPalm.Rotation, rightGrip.rotation),
                Vector3.Distance(leftPalm.Position, leftGrip.position),
                Quaternion.Angle(leftPalm.Rotation, leftGrip.rotation),
                Vector3.Distance(rightIk.position, rightGrip.position),
                Quaternion.Angle(rightIk.rotation, rightGrip.rotation),
                Vector3.Distance(leftIk.position, leftGrip.position),
                Quaternion.Angle(leftIk.rotation, leftGrip.rotation));
            return true;
        }

        private static Transform FindUnique(Transform root, string name)
        {
            Transform result = null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != name) continue;
                if (result != null) throw new InvalidOperationException("Duplicate transform: " + name);
                result = candidate;
            }

            return result != null ? result : throw new InvalidOperationException("Missing transform: " + name);
        }

        private static string Format(Quaternion value)
        {
            return $"({value.x:F7}, {value.y:F7}, {value.z:F7}, {value.w:F7})";
        }

        private readonly struct RuntimeMeasurement
        {
            public RuntimeMeasurement(
                KTransform rightPalm, KTransform leftPalm, Transform rightGrip, Transform leftGrip,
                float rightPositionError, float rightRotationError,
                float leftPositionError, float leftRotationError,
                float rightIkPositionError, float rightIkRotationError,
                float leftIkPositionError, float leftIkRotationError)
            {
                RightPalm = rightPalm;
                LeftPalm = leftPalm;
                RightGrip = rightGrip;
                LeftGrip = leftGrip;
                RightPositionError = rightPositionError;
                RightRotationError = rightRotationError;
                LeftPositionError = leftPositionError;
                LeftRotationError = leftRotationError;
                RightIkPositionError = rightIkPositionError;
                RightIkRotationError = rightIkRotationError;
                LeftIkPositionError = leftIkPositionError;
                LeftIkRotationError = leftIkRotationError;
            }

            public KTransform RightPalm { get; }
            public KTransform LeftPalm { get; }
            public Transform RightGrip { get; }
            public Transform LeftGrip { get; }
            public float RightPositionError { get; }
            public float RightRotationError { get; }
            public float LeftPositionError { get; }
            public float LeftRotationError { get; }
            public float RightIkPositionError { get; }
            public float RightIkRotationError { get; }
            public float LeftIkPositionError { get; }
            public float LeftIkRotationError { get; }
        }
    }
}
