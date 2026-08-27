using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static CGame.Animation.Editor.AnimationEditorConstants;

namespace CGame.Animation.Editor
{
    public partial class AnimationEditorWindow : EditorWindow
    {
        private const string DefaultPreviewSourcePath = "Assets/KINEMATION/ScriptableAnimationSystemDemo/Prefabs/Humanoid/FPSHumanoidPlayer.prefab";

        [SerializeField] private AnimationWindowState state = new AnimationWindowState();
        [SerializeField] private bool eventContextPanelOpen;
        [SerializeField] private Rect eventContextPanelRect;

        private SerializedObject serializedAsset;
        private Vector2 scroll;
        private PreviewRenderUtility previewUtility;
        private GameObject previewInstance;
        private readonly AnimationEventService eventService = new AnimationEventService();
        private int renamingTrackIndex = -1;
        private string renamingTrackName = string.Empty;

        public AnimationAssetBase EditingAsset => state.EditingAsset;
        public bool CanEditSelectedAsset => state.EditingAsset != null && state.EditingAsset.CanEditNotifies;
        public int PreviewFrame => state.PreviewFrame;
        public int SelectedNotifyTrackIndex => state.SelectedNotifyTrackIndex;
        public float TimelineFrameWidth => state.TimelineFrameWidth;
        public bool NotifyTracksExpanded => state.NotifyTracksExpanded;

        [MenuItem("CGame/Animation/Animation Editor")]
        public static AnimationEditorWindow Open()
        {
            var window = GetWindow<AnimationEditorWindow>("Animation Editor");
            window.SyncFromSelection();
            return window;
        }

        public static AnimationEditorWindow Open(AnimationAssetBase asset)
        {
            var window = GetWindow<AnimationEditorWindow>("Animation Editor");
            window.SetAsset(asset);
            return window;
        }

        public void SetAsset(AnimationAssetBase asset)
        {
            state.SetEditingAsset(asset);
            serializedAsset = asset == null ? null : new SerializedObject(asset);
            Repaint();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            if (state.EditingAsset == null)
            {
                SyncFromSelection();
            }
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            DestroyPreviewInstance();
            previewUtility?.Cleanup();
            previewUtility = null;
        }

        private void OnSelectionChanged()
        {
            SyncFromSelection();
            Repaint();
        }

        private void OnGUI()
        {
            if (state.EditingAsset == null)
            {
                EditorGUILayout.HelpBox("Select an AnimationClipAsset or AnimationSequenceAsset to edit notifies.", MessageType.Info);
                return;
            }

            if (serializedAsset == null || serializedAsset.targetObject != state.EditingAsset)
            {
                serializedAsset = new SerializedObject(state.EditingAsset);
            }

            serializedAsset.Update();
            HandleSelectedEventKeyboard();

            SerializedProperty tracks = serializedAsset.FindProperty("notifyTracks");
            DrawEditorSurface(tracks);

            DrawEventContextPanel();

            serializedAsset.ApplyModifiedProperties();
        }

        public void SetPreviewFrame(int frame)
        {
            state.SetPreviewFrame(frame);
            Repaint();
        }

        public void SetTimelineFrameWidth(float frameWidth)
        {
            state.SetTimelineFrameWidth(frameWidth);
            Repaint();
        }

        public void SetNotifyTracksExpanded(bool expanded)
        {
            state.SetNotifyTracksExpanded(expanded);
            Repaint();
        }

        private void SyncFromSelection()
        {
            if (Selection.activeObject is AnimationAssetBase selectedAsset)
            {
                SetAsset(selectedAsset);
            }
        }

        private void RebuildSerializedAsset()
        {
            serializedAsset = state.EditingAsset == null ? null : new SerializedObject(state.EditingAsset);
            Repaint();
        }

        private bool HasNotifyTrack(int trackIndex)
        {
            return state.EditingAsset != null && trackIndex >= 0 && trackIndex < state.EditingAsset.NotifyTracks.Count;
        }

        private AnimationNotifyEvent GetNotifyEvent(int trackIndex, int eventIndex)
        {
            if (!HasNotifyTrack(trackIndex))
            {
                return null;
            }

            AnimationNotifyTrack track = state.EditingAsset.NotifyTracks[trackIndex];
            return eventIndex >= 0 && eventIndex < track.Events.Count ? track.Events[eventIndex] : null;
        }

        private AnimationNotifyEvent GetSelectedNotifyEvent()
        {
            return GetNotifyEvent(state.SelectedNotifyEventTrack, state.SelectedNotifyEventIndex);
        }

        private void EnsureSelectedTrackInRange(SerializedProperty tracks)
        {
            state.EnsureSelectedTrackInRange();
        }
        
#if UNITY_INCLUDE_TESTS
        public AnimationNotifyEvent AddNotifyToTrackAtFrameForTesting(int trackIndex, int frame, bool durationEvent)
        {
            return AddNotifyToTrackAtFrame(trackIndex, durationEvent ? new AnimationDurationNotify() : new AnimationInstantNotify(), durationEvent ? DefaultNotifyStateDurationFrames : 0, frame);
        }

        public float FrameToTimelineXForTesting(int frame)
        {
            return TimeUtility.FrameToX(Rect.zero, frame, state.TimelineFrameWidth);
        }

        public int TimelineXToFrameForTesting(float x, int totalFrames)
        {
            return TimeUtility.XToFrame(Rect.zero, x, totalFrames, state.TimelineFrameWidth);
        }
        
        public static AnimationEditorDragMode GetTimelineEventDragModeForTesting(float localX, float eventWidth, bool durationEvent)
        {
            return GetTimelineEventDragMode(localX, eventWidth, durationEvent);
        }

        public static Type[] GetNotifyMenuTypesForTesting(bool duration)
        {
            return GetNotifyMenuTypes(duration).ToArray();
        }

        public static string GetNotifyLabelForTesting(AnimationNotify notify)
        {
            return NotifyLabelUtility.GetLabel(notify);
        }
#endif

    }
}
