using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PinABuildCost
{
    internal static class PinButtonSprites
    {
        private static Sprite outlinedPress;

        internal static Sprite OutlinedPress(Sprite selected, Sprite pressed)
        {
            if (outlinedPress != null) return outlinedPress;
            if (selected == null || pressed == null || selected.rect.size != pressed.rect.size ||
                !CanReadRectangle(selected) || !CanReadRectangle(pressed)) return pressed;

            var border = selected.border;
            int width = (int)selected.rect.width;
            int height = (int)selected.rect.height;
            int left = (int)border.x, bottom = (int)border.y;
            int middleWidth = width - left - (int)border.z;
            int middleHeight = height - bottom - (int)border.w;
            if (left < 2 || bottom < 2 || middleWidth <= 0 || middleHeight <= 0) return pressed;

            // The native selected plate has the stepped white and black edge.
            // Replace only its nine-slice center with the native pressed fill.
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                ReadSprite(pressed, width, height, texture);
                var pixels = texture.GetPixels();
                ReadSprite(selected, width, height, texture);
                for (int y = bottom; y < bottom + middleHeight; y++)
                    for (int x = left; x < left + middleWidth; x++)
                        texture.SetPixel(x, y, pixels[y * width + x]);
                texture.Apply(false, true);
                outlinedPress = Sprite.Create(texture, new Rect(0, 0, width, height),
                    new Vector2(0.5f, 0.5f), selected.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
                outlinedPress.name = "Pin Cost (Outlined Press)";
                return outlinedPress;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is UnityException)
            {
                Object.Destroy(texture);
                return pressed;
            }
        }

        private static bool CanReadRectangle(Sprite sprite) => !sprite.packed ||
            (sprite.packingMode == SpritePackingMode.Rectangle && sprite.packingRotation == SpritePackingRotation.None);

        private static void ReadSprite(Sprite sprite, int width, int height, Texture2D copy)
        {
            var rect = sprite.textureRect;
            var atlas = sprite.texture;
            var render = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(atlas, render, new Vector2(rect.width / atlas.width, rect.height / atlas.height),
                    new Vector2(rect.x / atlas.width, rect.y / atlas.height));
                RenderTexture.active = render;
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(render);
            }
        }

        internal static void Release()
        {
            if (outlinedPress == null) return;
            Object.Destroy(outlinedPress.texture);
            Object.Destroy(outlinedPress);
            outlinedPress = null;
        }
    }
}
