using System.Collections.Generic;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PinABuildCost
{
    public sealed class PinHud : MonoBehaviour
    {
        private RectTransform bar, frame, viewport;
        private RectTransform selection, tabs, book, empty;
        private CanvasGroup visibility;
        private bool sizeDirty;
        private Rect lastScreen, lastBar, lastReserved, lastObstacle;
        private readonly List<Image> headerDetails = new List<Image>();
        private Rect[] detailBounds;
        private bool lastSideHidden;
        private Text label, status;
        private readonly List<ChecklistMark> marks = new List<ChecklistMark>();
        private readonly List<Text> materials = new List<Text>();
        private readonly Vector3[] corners = new Vector3[4];

        internal static PinHud Create(UIHeader header)
        {
            var bar = header.transform.Find("PanelResources") as RectTransform;
            var style = bar?.Find("Wood/Amount")?.GetComponent<Text>();
            var canvas = header.GetComponentInParent<Canvas>();
            if (style == null || canvas == null) return null;
            var root = new GameObject("Pinned build cost", typeof(RectTransform), typeof(LayoutElement));
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            var hud = root.AddComponent<PinHud>();
            hud.visibility = root.AddComponent<CanvasGroup>();
            hud.visibility.interactable = false;
            hud.visibility.blocksRaycasts = false;
            hud.bar = bar;
            hud.viewport = (RectTransform)canvas.rootCanvas.transform;
            // These native controls extend below the header when their detail views are open.
            hud.TrackDetails(header.transform.Find("PanelFood/Dropwdown"));
            hud.TrackDetails(header.transform.Find("PanelTaxes/SpellContainerDISABLE"));
            hud.TrackDetails(header.transform.parent.Find("UpperLeft"));
            hud.detailBounds = new Rect[hud.headerDetails.Count];
            var overlays = OverlayManager.Instance;
            if (overlays != null)
            {
                hud.selection = overlays.GetOverlaySelection()?.parent as RectTransform;
                hud.tabs = overlays.GetOverlayTabsSelection() as RectTransform;
                hud.empty = overlays.GetOverlayEmpty() as RectTransform;
            }
            hud.frame = (RectTransform)root.transform;
            hud.frame.SetParent(bar, false);
            hud.frame.anchorMin = hud.frame.anchorMax = Vector2.zero;
            hud.frame.pivot = Vector2.one;
            var backdrop = root.AddComponent<Image>();
            var border = bar.GetComponent<Image>();
            if (border != null && border.sprite != null)
            {
                backdrop.sprite = border.sprite;
                backdrop.type = border.type;
                backdrop.pixelsPerUnitMultiplier = border.pixelsPerUnitMultiplier;
                backdrop.color = border.color;
            }
            else backdrop.color = new Color32(47, 34, 26, 248);
            backdrop.raycastTarget = false;
            var inset = new GameObject("Inset", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            inset.rectTransform.SetParent(hud.frame, false);
            inset.rectTransform.anchorMin = Vector2.zero;
            inset.rectTransform.anchorMax = Vector2.one;
            inset.rectTransform.offsetMin = new Vector2(5, 5);
            inset.rectTransform.offsetMax = new Vector2(-5, -5);
            inset.color = new Color32(48, 35, 25, 255);
            inset.raycastTarget = false;
            var child = new GameObject("Pinned target", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(hud.frame, false);
            hud.label = child.GetComponent<Text>();
            hud.label.font = style.font;
            hud.label.fontSize = 24;
            hud.label.fontStyle = style.fontStyle;
            // Resource counters change color at their storage limit. The pin is neutral text.
            hud.label.color = Color.white;
            hud.label.alignment = TextAnchor.UpperLeft;
            hud.label.supportRichText = false;
            hud.label.raycastTarget = false;
            var textRect = hud.label.rectTransform;
            textRect.anchorMin = textRect.anchorMax = textRect.pivot = Vector2.up;
            hud.status = Instantiate(hud.label, hud.frame);
            hud.status.name = "Purchase status";
            return hud;
        }

        internal void Show(string title, CostChecklist checklist)
        {
            gameObject.SetActive(true);
            sizeDirty |= label.text != title || status.text != (checklist.Status ?? "");
            label.text = title;
            status.text = checklist.Status ?? "";
            status.gameObject.SetActive(checklist.Status != null);
            for (int i = 0; i < checklist.Items.Length; i++)
            {
                if (i == materials.Count)
                {
                    var caption = Instantiate(label, frame);
                    caption.name = "Material";
                    var mark = new GameObject("Checkbox", typeof(RectTransform), typeof(ChecklistMark)).GetComponent<ChecklistMark>();
                    mark.rectTransform.SetParent(caption.rectTransform, false);
                    mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = mark.rectTransform.pivot = Vector2.up;
                    mark.rectTransform.anchoredPosition = new Vector2(-23, -3);
                    mark.rectTransform.sizeDelta = new Vector2(18, 18);
                    mark.raycastTarget = false;
                    materials.Add(caption);
                    marks.Add(mark);
                    sizeDirty = true;
                }
                var item = checklist.Items[i];
                var row = materials[i];
                sizeDirty |= !row.gameObject.activeSelf || row.text != item.Text;
                row.gameObject.SetActive(true);
                row.text = item.Text;
                marks[i].IsMet = item.IsMet;
            }
            for (int i = checklist.Items.Length; i < materials.Count; i++)
            {
                sizeDirty |= materials[i].gameObject.activeSelf;
                materials[i].gameObject.SetActive(false);
            }
            if (book == null) book = bar.Find("Daybook") as RectTransform;
            Position();
        }

        private void LateUpdate()
        {
            if (label != null) Position();
        }

        private void Position()
        {
            var screen = Bounds(viewport);
            var reserved = book != null && book.gameObject.activeInHierarchy ? Bounds(book) : bar.rect;
            float top = bar.rect.yMin - 6;
            // Keep the side HUDs clear. Check the preferred side first, then the opposite one.
            float inset = screen.width * 0.16f;
            var panel = selection != null && selection.gameObject.activeInHierarchy ? selection : tabs;
            if (empty != null && empty.gameObject.activeInHierarchy && empty.childCount > 0)
                panel = empty.GetChild(empty.childCount - 1) as RectTransform;
            var obstacle = panel != null && panel.gameObject.activeInHierarchy ? Bounds(panel) : Rect.zero;
            bool detailsChanged = UpdateDetailBounds();
            bool sideHidden = UIManager.Instance != null && UIManager.Instance.IsHidden();
            if (!sizeDirty && !detailsChanged && sideHidden == lastSideHidden && screen == lastScreen && bar.rect == lastBar && reserved == lastReserved &&
                obstacle == lastObstacle) return;
            lastSideHidden = sideHidden;
            lastScreen = screen;
            lastBar = bar.rect;
            lastReserved = reserved;
            lastObstacle = obstacle;
            bool placed = Place(screen.xMin + (sideHidden ? 8 : inset), Mathf.Min(bar.rect.xMin, reserved.xMin) - 6,
                top, screen.yMin, obstacle, true) ||
                Place(Mathf.Max(bar.rect.xMax, reserved.xMax) + 6, screen.xMax - inset,
                    top, screen.yMin, obstacle, false);
            visibility.alpha = placed ? 1 : 0;
            sizeDirty = false;
        }

        private void TrackDetails(Transform group)
        {
            if (group == null) return;
            foreach (var image in group.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && image.sprite.border != Vector4.zero) headerDetails.Add(image);
        }

        private bool UpdateDetailBounds()
        {
            bool changed = false;
            for (int i = 0; i < headerDetails.Count; i++)
            {
                var detail = headerDetails[i];
                var bounds = detail != null && detail.isActiveAndEnabled ? Bounds(detail.rectTransform) : Rect.zero;
                changed |= detailBounds[i] != bounds;
                detailBounds[i] = bounds;
            }
            return changed;
        }

        private Rect Bounds(RectTransform item)
        {
            item.GetWorldCorners(corners);
            Vector2 min = bar.InverseTransformPoint(corners[0]);
            Vector2 max = bar.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private bool Place(float left, float right, float top, float bottom, Rect obstacle, bool alignRight)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                float width = Mathf.Min(300, Mathf.Floor(right - left));
                if (width < 112) return false;
                Resize(width);
                float height = frame.rect.height;
                float x = alignRight ? right - width : left;
                float y = top;
                foreach (var detail in detailBounds)
                    if (detail.width > 0 && x < detail.xMax + 6 && x + width > detail.xMin - 6 && detail.yMin < top)
                        y = Mathf.Min(y, detail.yMin - 6);
                if (y - height < bottom + 6) return false;
                if (!new Rect(x, y - height, width, height).Overlaps(obstacle) || obstacle.width == 0)
                {
                    frame.anchoredPosition = new Vector2(Mathf.Round(x + width - bar.rect.xMin), Mathf.Floor(y - bar.rect.yMin));
                    return true;
                }
                if (alignRight) right = obstacle.xMin - 6;
                else left = obstacle.xMax + 6;
            }
            return false;
        }

        private void Resize(float width)
        {
            if (sizeDirty || !Mathf.Approximately(frame.rect.width, width))
            {
                frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                float y = 8;
                void PlaceLine(Text line, int left)
                {
                    var rect = line.rectTransform;
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - left - 12);
                    float height = Mathf.Ceil(line.preferredHeight);
                    rect.anchoredPosition = new Vector2(left, -y);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    y += height + 2;
                }
                PlaceLine(label, 12);
                foreach (var row in materials)
                    if (row.gameObject.activeSelf) PlaceLine(row, 35);
                if (status.gameObject.activeSelf) PlaceLine(status, 12);
                frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + 6);
                sizeDirty = false;
            }
        }
    }
}
