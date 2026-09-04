using UnityEngine;
using TMPro;

namespace CGame.Network
{
    public sealed class OwnerGameplayHud : MonoBehaviour
    {
        private readonly OwnerGameplayHudState state = new OwnerGameplayHudState();
        private TextMeshProUGUI label;

        public string Text => state.Text;
        public bool Visible => state.Visible;

        public void Reset() => state.Reset();

        public void Apply(OwnerGameplayStateEvent message, bool isLocallyPossessed)
        {
            if (message == null) return;
            state.Apply(
                new OwnerGameplayVitals(message.PawnId, message.VitalsRevision, message.Health, message.MaxHealth, message.IsDead),
                new OwnerGameplayEquipment(message.PawnId, message.EquipmentRevision, message.WeaponName, message.MagazineAmmo, message.MagazineCapacity),
                isLocallyPossessed);
        }

        private void Awake()
        {
            var canvasRoot = new GameObject("OwnerGameplayHudCanvas", typeof(Canvas));
            canvasRoot.transform.SetParent(transform, false);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var labelRoot = new GameObject("OwnerGameplayHudText", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelRoot.transform.SetParent(canvasRoot.transform, false);
            label = labelRoot.GetComponent<TextMeshProUGUI>();
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.TopRight;
            label.color = Color.white;
            RectTransform rect = label.rectTransform;
            // The lobby debug panel owns the upper-left corner.  Keep authority
            // vitals independently visible rather than layering underneath it.
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(720f, 48f);
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (label == null) return;
            label.enabled = state.Visible;
            label.text = state.Text;
        }
    }
}
