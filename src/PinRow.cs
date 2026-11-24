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
        private void OnDisable()
        {
            PinPlugin.Instance?.Leave(this);
            // An individually withdrawn card may be pooled for a different offer.
            // Hiding the whole panel or tab leaves its bindings available on return.
            if (!gameObject.activeSelf && Row != null && Row.gameObject != gameObject)
                Row.RetireRow();
        }
        private void OnDestroy()
        {
            if (Row != null && Row.gameObject != gameObject) Row.RetireRow();
        }
    }

    public sealed class PinRow : MonoBehaviour
    {
        private PinTarget target;
        private TMP_Text caption;
        private Image glyph;
        private PinPlugin owner;
        private Button purchaseButton;
        private bool retired;
        internal bool UseAsFocusFallback;
        internal bool HasVisibleOffer => !retired && cardRect != null && purchaseButton != null &&
            cardRect.gameObject.activeInHierarchy && purchaseButton.isActiveAndEnabled;
        private bool showShortcut;
        private InputAction shortcut;
        private string displayedPath;
        private string bindingText;
        private bool shortcutSupported;
        private static Button nativeButton;
        private RectTransform face, actionStrip, leftButton, rightButton, cardRect;
        private LayoutElement dialogStripLayout;
        private float originalStripWidth, reservedStripWidth = -1;
        private float faceWidth;
        private readonly Vector3[] corners = new Vector3[4];

        internal static PinRow Create(GameObject card, Button purchase, PinTarget target,
            RectTransform actionStrip = null, RectTransform leftButton = null, RectTransform rightButton = null)
        {
            PinPlugin.Instance.RefreshOffer(target);
            var existing = purchase.GetComponent<PinCardLink>()?.Row;
            if (existing != null && !existing.retired && existing.cardRect == card.transform && existing.purchaseButton == purchase)
            {
                existing.target = target;
                existing.Label();
                return existing;
            }
            if (existing != null) existing.RetireRow();
            var root = new GameObject("Pin cost · " + target.Title, typeof(RectTransform));
            root.SetActive(false);
            var rect = (RectTransform)root.transform;
            rect.SetParent(card.transform.parent, false);
            rect.SetSiblingIndex(card.transform.GetSiblingIndex() + 1);
            rect.sizeDelta = new Vector2(((RectTransform)card.transform).rect.width, 44);
            var size = root.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 44;
            size.flexibleWidth = 1;
            var surface = new GameObject("Button", typeof(RectTransform), typeof(Image));
            var face = (RectTransform)surface.transform;
            face.SetParent(rect, false);
            var bg = surface.GetComponent<Image>();
            var button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            if (nativeButton == null)
                nativeButton = Resources.Load<GameObject>("UI/OverlayMessage")?.transform.Find("Buttons/Cancel")?.GetComponent<Button>();
            var style = nativeButton != null ? nativeButton : purchase;
            if (style.targetGraphic is Image sourceImage)
            {
                bg.sprite = sourceImage.sprite;
                bg.type = sourceImage.type;
                bg.color = sourceImage.color;
                bg.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
            }
            button.colors = style.colors;
            button.spriteState = style.spriteState;
            button.transition = style.transition == Selectable.Transition.SpriteSwap
                ? Selectable.Transition.SpriteSwap : Selectable.Transition.ColorTint;

            var line = surface.AddComponent<HorizontalLayoutGroup>();
            line.childAlignment = TextAnchor.MiddleCenter;
            line.spacing = 4;
            line.padding = new RectOffset(8, 8, 0, 0);
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;
            var row = root.AddComponent<PinRow>();
            row.target = target;
            row.owner = PinPlugin.Instance;
            row.purchaseButton = purchase;
            row.face = face;
            row.cardRect = (RectTransform)card.transform;
            row.actionStrip = actionStrip;
            row.leftButton = leftButton;
            row.rightButton = rightButton;
            if (leftButton != null && rightButton != null && actionStrip == card.transform)
                row.dialogStripLayout = actionStrip.GetComponent<LayoutElement>();
            var image = new GameObject("Binding", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            image.transform.SetParent(face, false);
            row.glyph = image.GetComponent<Image>();
            row.glyph.preserveAspect = true;
            row.glyph.raycastTarget = false;
            image.GetComponent<LayoutElement>().preferredWidth = 26;
            image.GetComponent<LayoutElement>().preferredHeight = 26;
            var text = new GameObject("Action", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(face, false);
            row.caption = text.GetComponent<TextMeshProUGUI>();
            var source = style.GetComponentInChildren<TMP_Text>(true);
            if (source != null)
            {
                row.caption.font = source.font;
                row.caption.fontSharedMaterial = source.fontSharedMaterial;
                row.caption.color = source.color;
            }
            row.caption.fontSize = source != null ? source.fontSize : 32;
            row.caption.enableWordWrapping = false;
            row.caption.overflowMode = TextOverflowModes.Ellipsis;
            row.caption.raycastTarget = false;
            row.caption.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(row.Activate);

            AddLink(root, row);
            AddLink(purchase.gameObject, row);
            // A confirmation dialog shares its action strip with Cancel and other actions.
            if (purchase.gameObject != card && card.transform != actionStrip) AddLink(card, row);
            root.SetActive(true);
            return row;
        }

        private static void AddLink(GameObject obj, PinRow row)
        {
            var link = obj.GetComponent<PinCardLink>() ?? obj.AddComponent<PinCardLink>();
            link.Row = row;
        }

        internal void RetireRow()
        {
            if (retired) return;
            retired = true;
            if (gameObject.activeInHierarchy) gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnEnable()
        {
            if (owner == null) return;
            owner.Register(this);
            owner.Changed += Label;
            GamepadManager.OnControlSchemeChanged += SchemeChanged;
            Bind();
        }

        private void Start()
        {
            // A later purchase row may be affordable. Let the native panel finish drawing
            // and choose it before offering a pin as the controller's initial selection.
            if (UseAsFocusFallback && HasVisibleOffer && GamepadManager.Instance != null &&
                GamepadManager.Instance.GetGameObjectToSelect() == null)
                GamepadManager.Instance.SetGameObjectToSelect(gameObject);
        }

        private void OnDisable()
        {
            if (dialogStripLayout != null && dialogStripLayout.preferredWidth == reservedStripWidth && reservedStripWidth >= 0)
                dialogStripLayout.preferredWidth = originalStripWidth;
            reservedStripWidth = -1;
            if (owner != null)
            {
                owner.Unregister(this);
                owner.Changed -= Label;
            }
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
            shortcutSupported = SupportsShortcut(shortcut, index);
            glyph.sprite = shortcutSupported ? pad.GetButtonPrompt(displayedPath) : null;
            bindingText = shortcutSupported && glyph.sprite == null ? shortcut.GetBindingDisplayString(index) : null;
            Label();
        }

        internal void SetShortcutHint(bool show)
        {
            showShortcut = show;
            Label();
        }

        private void Label()
        {
            string action = target.Action(owner.Pinned);
            glyph.gameObject.SetActive(showShortcut && glyph.sprite != null);
            caption.text = showShortcut && !string.IsNullOrEmpty(bindingText) ? bindingText + "  " + action : action;
            var line = face.GetComponent<HorizontalLayoutGroup>();
            float textWidth = caption.preferredWidth;
            bool reserveGlyph = glyph.gameObject.activeSelf;
            if (leftButton != null && rightButton != null)
            {
                // Reserve the largest action before a pin or focus change sizes the dialog.
                var longest = string.IsNullOrEmpty(bindingText) ? PinTarget.ReplaceLabel : bindingText + "  " + PinTarget.ReplaceLabel;
                textWidth = Mathf.Max(textWidth, caption.GetPreferredValues(longest).x);
                reserveGlyph = glyph.sprite != null;
            }
            float promptWidth = reserveGlyph ? glyph.GetComponent<LayoutElement>().preferredWidth + line.spacing : 0;
            faceWidth = Mathf.Max(84, Mathf.Ceil(textWidth + promptWidth) + line.padding.horizontal + 4);
            FitFace();
        }

        private void OnRectTransformDimensionsChange() => FitFace();

        private void FitFace()
        {
            if (face == null || faceWidth <= 0) return;
            float width = Mathf.Min(faceWidth, ((RectTransform)transform).rect.width);
            face.sizeDelta = new Vector2(Mathf.Max(0, width), 38);
        }

        private void LateUpdate()
        {
            if (!HasVisibleOffer)
            {
                RetireRow();
                return;
            }
            if (actionStrip == null || rightButton == null) return;
            // Use only the empty space between existing controls. Recheck after native layout,
            // rebinding, or another mod inserting a sibling into the same strip.
            var rect = (RectTransform)transform;
            bool nested = rect.parent == actionStrip;
            if (dialogStripLayout != null && leftButton != null && actionStrip.childCount == (nested ? 3 : 2))
            {
                // The native message uses a ContentSizeFitter. Its end buttons grow toward
                // the center on focus, so give each its own gutter at full label size.
                float buyWidth = Mathf.Max(leftButton.rect.width, LayoutUtility.GetPreferredWidth(leftButton));
                float cancelWidth = Mathf.Max(rightButton.rect.width, LayoutUtility.GetPreferredWidth(rightButton));
                // UIClickable.PunchScale can enlarge either end button by 25% about its outer edge.
                float gutter = Mathf.Ceil(Mathf.Max(buyWidth, cancelWidth) * 0.25f) + 6;
                float required = Mathf.Ceil(buyWidth + faceWidth + cancelWidth + 2 * gutter);
                if (dialogStripLayout.preferredWidth < required)
                {
                    if (dialogStripLayout.preferredWidth != reservedStripWidth || reservedStripWidth < 0)
                        originalStripWidth = dialogStripLayout.preferredWidth;
                    dialogStripLayout.preferredWidth = reservedStripWidth = required;
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)actionStrip.parent);
                }
            }
            rightButton.GetWorldCorners(corners);
            Vector2 right = actionStrip.InverseTransformPoint(corners[0]);
            float left = actionStrip.rect.xMin + 2;
            if (leftButton != null)
            {
                leftButton.GetWorldCorners(corners);
                left = actionStrip.InverseTransformPoint(corners[2]).x + 4;
            }
            bool fits = actionStrip.childCount == 1 + (leftButton == null ? 0 : 1) + (nested ? 1 : 0) &&
                right.x - left >= faceWidth + 4 && actionStrip.rect.height >= 38;
            if (fits && !nested)
            {
                GetComponent<LayoutElement>().ignoreLayout = true;
                rect.SetParent(actionStrip, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            }
            if (fits)
            {
                float x = leftButton == null ? left : (left + right.x - 4 - faceWidth) / 2;
                float y = right.y;
                if (dialogStripLayout != null && leftButton != null)
                {
                    // Align to the resting positions, independent of the native focus pulse.
                    float leftEdge = leftButton.localPosition.x + leftButton.rect.xMax;
                    float rightEdge = rightButton.localPosition.x + rightButton.rect.xMin;
                    x = (leftEdge + rightEdge - faceWidth) / 2;
                    y = rightButton.localPosition.y + rightButton.rect.center.y - 19;
                }
                rect.anchoredPosition = new Vector2(Mathf.Round(x - actionStrip.rect.xMin),
                    Mathf.Round(y - actionStrip.rect.yMin));
                rect.sizeDelta = new Vector2(faceWidth, 38);
            }
            else if (nested)
            {
                rect.SetParent(cardRect.parent, false);
                GetComponent<LayoutElement>().ignoreLayout = false;
                rect.SetSiblingIndex(cardRect.GetSiblingIndex() + 1);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(cardRect.rect.width, 44);
            }
        }

        internal void CheckShortcut(GamepadManager pad)
        {
            // Refresh effective bindings on focus as players can rebind without changing device.
            var input = pad.GetPlayerInput();
            if (input == null || string.IsNullOrEmpty(input.currentControlScheme)) return;
            int index = shortcut != null ? shortcut.GetBindingIndex(input.currentControlScheme) : -1;
            string path = index < 0 ? null : shortcut.bindings[index].effectivePath;
            if (path != displayedPath || shortcutSupported != SupportsShortcut(shortcut, index)) Bind();
            if (!shortcutSupported) return;
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

        internal static bool SupportsShortcut(InputAction action, int index)
        {
            if (action == null || index < 0) return false;
            return SupportsBinding(action.enabled, action.interactions, action.bindings[index]);
        }

        internal static bool SupportsBinding(bool enabled, string interactions, InputBinding binding) =>
            !string.IsNullOrEmpty(binding.effectivePath) && (enabled ||
                (string.IsNullOrEmpty(interactions) && string.IsNullOrEmpty(binding.effectiveInteractions)
                 && !binding.isComposite && !binding.isPartOfComposite));

        private void Activate()
        {
            if (owner != null && HasVisibleOffer && target.Source != null) owner.Activate(target);
        }
    }
}
