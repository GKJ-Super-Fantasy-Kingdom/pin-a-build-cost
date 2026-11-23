using SuperFantasyKingdom;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace PinABuildCost
{
    public sealed class PinCardLink : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal PinRow Row;
        public void OnPointerEnter(PointerEventData eventData) => PinPlugin.Instance?.Hover(this);
        public void OnPointerExit(PointerEventData eventData) => PinPlugin.Instance?.Leave(this);
        private void OnDisable() => PinPlugin.Instance?.Leave(this);
    }

    public sealed class PinRow : MonoBehaviour
    {
        private PinTarget target;
        private TMP_Text caption;
        private Image glyph;
        private PinPlugin owner;
        private int activatedFrame = -1;
        private InputAction shortcut;
        private string displayedPath;
        private string bindingText;

        internal static PinRow Create(GameObject card, Button purchase, PinTarget target)
        {
            var root = new GameObject("Pin cost · " + target.Title, typeof(RectTransform));
            root.SetActive(false);
            var rect = (RectTransform)root.transform;
            rect.SetParent(card.transform.parent, false);
            rect.SetSiblingIndex(card.transform.GetSiblingIndex() + 1);
            rect.sizeDelta = new Vector2(((RectTransform)card.transform).rect.width, 32);
            var size = root.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 32;
            size.flexibleWidth = 1;
            var bg = root.AddComponent<Image>();
            var button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = new Color32(62, 48, 37, 255);
            colors.highlightedColor = colors.selectedColor = new Color32(107, 78, 40, 255);
            colors.pressedColor = new Color32(130, 90, 48, 255);
            button.colors = colors;

            var line = root.AddComponent<HorizontalLayoutGroup>();
            line.childAlignment = TextAnchor.MiddleCenter;
            line.spacing = 6;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;
            var row = root.AddComponent<PinRow>();
            row.target = target;
            row.owner = PinPlugin.Instance;
            var image = new GameObject("Binding", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            image.transform.SetParent(rect, false);
            row.glyph = image.GetComponent<Image>();
            row.glyph.preserveAspect = true;
            row.glyph.raycastTarget = false;
            image.GetComponent<LayoutElement>().preferredWidth = 26;
            image.GetComponent<LayoutElement>().preferredHeight = 26;
            var text = new GameObject("Action", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(rect, false);
            row.caption = text.GetComponent<TextMeshProUGUI>();
            var source = card.GetComponentInChildren<TMP_Text>(true);
            if (source != null) row.caption.font = source.font;
            row.caption.fontSize = 20;
            row.caption.color = new Color32(255, 232, 181, 255);
            row.caption.raycastTarget = false;
            row.caption.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(row.Activate);

            AddLink(root, row);
            AddLink(purchase.gameObject, row);
            if (purchase.gameObject != card) AddLink(card, row);
            root.SetActive(true);
            return row;
        }

        private static void AddLink(GameObject obj, PinRow row)
        {
            var link = obj.GetComponent<PinCardLink>() ?? obj.AddComponent<PinCardLink>();
            link.Row = row;
        }

        private void OnEnable()
        {
            if (owner == null) return;
            owner.Changed += Label;
            GamepadManager.OnControlSchemeChanged += SchemeChanged;
            Bind();
        }

        private void OnDisable()
        {
            if (owner != null) owner.Changed -= Label;
            GamepadManager.OnControlSchemeChanged -= SchemeChanged;
        }

        private void SchemeChanged(string current, string previous) => Bind();

        private void Bind()
        {
            var pad = GamepadManager.Instance;
            var input = pad == null ? null : pad.GetPlayerInput();
            shortcut = input == null ? null : input.actions.FindAction("UI/Tab");
            displayedPath = null;
            int index = input == null || string.IsNullOrEmpty(input.currentControlScheme) ||
                shortcut == null ? -1 : shortcut.GetBindingIndex(input.currentControlScheme);
            if (index >= 0) displayedPath = shortcut.bindings[index].effectivePath;
            glyph.sprite = string.IsNullOrEmpty(displayedPath) ? null : pad.GetButtonPrompt(displayedPath);
            glyph.gameObject.SetActive(glyph.sprite != null);
            bindingText = index >= 0 && glyph.sprite == null ? shortcut.GetBindingDisplayString(index) : null;
            Label();
        }

        private void Label()
        {
            string action = target.Action(owner.Pinned);
            caption.text = string.IsNullOrEmpty(bindingText) ? action : bindingText + "  " + action;
        }

        internal void CheckShortcut(GamepadManager pad)
        {
            // Refresh effective bindings on focus as players can rebind without changing device.
            var input = pad.GetPlayerInput();
            if (input == null || string.IsNullOrEmpty(input.currentControlScheme)) return;
            int index = shortcut != null ? shortcut.GetBindingIndex(input.currentControlScheme) : -1;
            string path = index < 0 ? null : shortcut.bindings[index].effectivePath;
            if (shortcut == null || path != displayedPath) Bind();
            if (string.IsNullOrEmpty(displayedPath) || shortcut == null) return;
            if (shortcut.enabled)
            {
                if (shortcut.WasPerformedThisFrame()) Activate();
                return;
            }

            // Some screens enable only the UI module's navigation actions. Read the
            // player's Tab binding there without enabling a map or changing game controls.
            foreach (var device in input.devices)
            {
                var control = InputControlPath.TryFindControl<ButtonControl>(device, displayedPath);
                if (control != null && control.wasPressedThisFrame)
                {
                    Activate();
                    break;
                }
            }
        }

        private void Activate()
        {
            if (activatedFrame == Time.frameCount || owner == null) return;
            activatedFrame = Time.frameCount;
            owner.Toggle(target);
        }
    }
}
