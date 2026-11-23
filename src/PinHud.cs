using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PinABuildCost
{
    public sealed class PinHud : MonoBehaviour
    {
        private RectTransform bar, frame, viewport;
        private Text label;
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
            hud.bar = bar;
            hud.viewport = (RectTransform)canvas.rootCanvas.transform;
            hud.frame = (RectTransform)root.transform;
            hud.frame.SetParent(bar, false);
            hud.frame.anchorMin = hud.frame.anchorMax = Vector2.zero;
            hud.frame.pivot = Vector2.one;
            var backdrop = root.AddComponent<Image>();
            backdrop.color = new Color32(47, 34, 26, 248);
            backdrop.raycastTarget = false;
            var child = new GameObject("Shortages", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(hud.frame, false);
            hud.label = child.GetComponent<Text>();
            hud.label.font = style.font;
            hud.label.fontSize = 22;
            hud.label.fontStyle = style.fontStyle;
            hud.label.color = new Color32(255, 233, 185, 255);
            hud.label.alignment = TextAnchor.UpperLeft;
            hud.label.supportRichText = false;
            hud.label.raycastTarget = false;
            var textRect = hud.label.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(9, 6);
            textRect.offsetMax = new Vector2(-9, -6);
            return hud;
        }

        internal void Show(string title, string shortages)
        {
            gameObject.SetActive(true);
            float right = bar.rect.xMin;
            var book = bar.Find("Daybook") as RectTransform;
            if (book != null && book.gameObject.activeInHierarchy)
            {
                book.GetWorldCorners(corners);
                right = Mathf.Min(right, bar.InverseTransformPoint(corners[0]).x);
            }
            viewport.GetWorldCorners(corners);
            float left = bar.InverseTransformPoint(corners[0]).x;
            float viewportWidth = bar.InverseTransformPoint(corners[2]).x - left;
            float available = right - left - 14;
            float width = Mathf.Min(300, Mathf.Max(100, available - viewportWidth * 0.16f));
            frame.anchoredPosition = new Vector2(right - bar.rect.xMin - 6, -6);
            frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            label.text = title + "\n" + shortages;
            frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(label.preferredHeight) + 14);
        }
    }
}
