using System;
using TMPro;
using UnityEngine;

namespace CGame.Network
{
    public sealed class OwnerGameplayTerminalPresenter : MonoBehaviour
    {
        private long pawnId;
        private long lastVitalsRevision = -1;
        private Action enterTerminal;
        private Action exitSession;
        private bool exitRequested;

        public bool IsDead { get; private set; }

        public void Configure(long ownerPawnId, Action onDeath, Action onExit)
        {
            pawnId = ownerPawnId;
            enterTerminal = onDeath ?? throw new ArgumentNullException(nameof(onDeath));
            exitSession = onExit ?? throw new ArgumentNullException(nameof(onExit));
        }

        public void Apply(OwnerGameplayStateEvent state)
        {
            if (IsDead || state == null || pawnId <= 0 || state.PawnId != pawnId ||
                state.VitalsRevision <= lastVitalsRevision || state.MaxHealth <= 0 ||
                state.Health < 0 || state.Health > state.MaxHealth || state.IsDead != (state.Health == 0)) return;
            lastVitalsRevision = state.VitalsRevision;
            if (!state.IsDead) return;
            IsDead = true;
            enterTerminal();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var canvasRoot = new GameObject("OwnerDeathCanvas", typeof(Canvas));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var textRoot = new GameObject("OwnerDeathResult", typeof(RectTransform), typeof(TextMeshProUGUI));
            textRoot.transform.SetParent(canvasRoot.transform, false);
            var label = textRoot.GetComponent<TextMeshProUGUI>();
            label.text = "YOU DIED\n<size=24>Combat input disabled</size>";
            label.fontSize = 52f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            label.rectTransform.sizeDelta = new Vector2(640f, 150f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 70f);
        }

        private void OnGUI()
        {
            if (!IsDead || exitRequested) return;
            if (GUI.Button(new Rect(Screen.width * .5f - 100f, Screen.height * .5f + 45f, 200f, 40f), "Exit session"))
                RequestExit();
        }

        public void RequestExit()
        {
            if (!IsDead || exitRequested) return;
            exitRequested = true;
            exitSession();
        }
    }
}
