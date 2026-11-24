using UnityEngine;
using UnityEngine.UI;

namespace PinABuildCost
{
    // Draw on a small pixel grid so the check is readable even when the HUD font lacks that glyph.
    public sealed class ChecklistMark : MaskableGraphic
    {
        private bool isMet;
        internal bool IsMet
        {
            get => isMet;
            set
            {
                if (isMet == value) return;
                isMet = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            void Patch(int x, int y, int w, int h, Color32 tint)
            {
                float left = rect.xMin + x * rect.width / 10;
                float bottom = rect.yMin + y * rect.height / 10;
                float right = left + w * rect.width / 10;
                float top = bottom + h * rect.height / 10;
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
                mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
                mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
                mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }

            Patch(0, 0, 10, 10, isMet ? new Color32(167, 204, 118, 255) : new Color32(194, 163, 119, 255));
            Patch(1, 1, 8, 8, new Color32(48, 35, 25, 255));
            if (!isMet) return;
            var tick = new Color32(183, 227, 138, 255);
            Patch(2, 4, 2, 2, tick);
            Patch(3, 3, 2, 2, tick);
            for (int step = 0; step < 4; step++) Patch(4 + step, 3 + step, 2, 2, tick);
        }
    }
}
