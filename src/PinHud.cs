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
        private Image[] selectionHeader, tabsHeader;
        private CanvasGroup visibility;
        private bool sizeDirty;
        private Rect lastScreen, lastBar, lastReserved, lastObstacle;
        private readonly List<Image> headerDetails = new List<Image>();
        private Rect[] detailBounds;
        private bool lastSideHidden;
        private Text label, status;
        private Image statusRule;
        private sealed class MaterialRow
        {
            internal Text Caption;
            internal ChecklistMark Mark;
            internal Image Icon;
        }
        private readonly List<MaterialRow> materials = new List<MaterialRow>();
        private static readonly Color32 MetColor = new Color32(189, 228, 155, 255);
        private static readonly Color32 ShortColor = new Color32(255, 207, 160, 255);
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
                hud.selectionHeader = HeaderImages(hud.selection);
                hud.tabsHeader = HeaderImages(hud.tabs);
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
            hud.label.color = new Color32(255, 241, 212, 255);
            hud.label.alignment = TextAnchor.UpperLeft;
            hud.label.supportRichText = false;
            hud.label.raycastTarget = false;
            var textRect = hud.label.rectTransform;
            textRect.anchorMin = textRect.anchorMax = textRect.pivot = Vector2.up;
            hud.status = Instantiate(hud.label, hud.frame);
            hud.status.name = "Purchase status";
            hud.statusRule = new GameObject("Status divider", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            hud.statusRule.rectTransform.SetParent(hud.frame, false);
            hud.statusRule.rectTransform.anchorMin = hud.statusRule.rectTransform.anchorMax =
                hud.statusRule.rectTransform.pivot = Vector2.up;
            hud.statusRule.color = new Color32(159, 120, 83, 255);
            hud.statusRule.raycastTarget = false;
            return hud;
        }

        internal void Show(string title, CostChecklist checklist)
        {
            gameObject.SetActive(true);
            sizeDirty |= label.text != title || status.text != (checklist.Status ?? "");
            label.text = title;
            status.text = checklist.Status ?? "";
            status.color = checklist.Status == CostChecklist.Ready ? MetColor : ShortColor;
            status.gameObject.SetActive(checklist.Status != null);
            statusRule.enabled = checklist.Status != null && checklist.Items.Length > 0;
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
                    var icon = new GameObject("Resource icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    icon.rectTransform.SetParent(caption.rectTransform, false);
                    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = Vector2.up;
                    icon.rectTransform.sizeDelta = new Vector2(20, 20);
                    icon.rectTransform.anchoredPosition = new Vector2(-24, -1);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    materials.Add(new MaterialRow { Caption = caption, Mark = mark, Icon = icon });
                    sizeDirty = true;
                }
                var item = checklist.Items[i];
                var row = materials[i];
                var sprite = RaceManager.Instance.GetResourceIcon(item.Resource);
                sizeDirty |= !row.Caption.gameObject.activeSelf || row.Caption.text != item.Text ||
                    (row.Icon.sprite == null) != (sprite == null);
                row.Caption.gameObject.SetActive(true);
                row.Caption.text = item.Text;
                row.Caption.color = item.IsMet ? MetColor : ShortColor;
                row.Mark.IsMet = item.IsMet;
                row.Icon.sprite = sprite;
            }
            for (int i = checklist.Items.Length; i < materials.Count; i++)
            {
                sizeDirty |= materials[i].Caption.gameObject.activeSelf;
                materials[i].Caption.gameObject.SetActive(false);
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
            var obstacle = PanelBounds(panel);
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

        private static Image[] HeaderImages(RectTransform panel) =>
            panel?.parent.Find("HeaderContainer/Header")?.GetComponentsInChildren<Image>(true);

        private Rect PanelBounds(RectTransform panel)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return Rect.zero;
            var bounds = Bounds(panel);
            var images = panel == selection ? selectionHeader : panel == tabs ? tabsHeader : null;
            // The scrollwork of a native title extends beyond its panel body.
            if (images != null)
                foreach (var image in images)
                    if (image != null && image.isActiveAndEnabled && image.sprite != null)
                    {
                        var part = Bounds(image.rectTransform);
                        bounds.min = Vector2.Min(bounds.min, part.min);
                        bounds.max = Vector2.Max(bounds.max, part.max);
                    }
            return bounds;
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
                if (obstacle.width == 0 || !new Rect(x - 6, y - height - 6, width + 12, height + 12).Overlaps(obstacle))
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
                {
                    if (!row.Caption.gameObject.activeSelf) continue;
                    bool showIcon = width >= 200 && row.Icon.sprite != null;
                    row.Icon.enabled = showIcon;
                    int left = showIcon ? 59 : 35;
                    row.Mark.rectTransform.anchoredPosition = new Vector2(12 - left, -3);
                    PlaceLine(row.Caption, left);
                }
                if (statusRule.enabled)
                {
                    statusRule.rectTransform.sizeDelta = new Vector2(width - 24, 1);
                    statusRule.rectTransform.anchoredPosition = new Vector2(12, -Mathf.Ceil(y + 1));
                    y += 5;
                }
                if (status.gameObject.activeSelf) PlaceLine(status, 12);
                frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + 6);
                sizeDirty = false;
            }
        }
    }
}
