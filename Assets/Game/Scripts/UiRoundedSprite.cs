using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku
{
    /// <summary>
    /// 运行时生成 9-slice 圆角矩形 sprite 的共享工具（按半径缓存）。
    /// </summary>
    public static class UiRoundedSprite
    {
        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        public static Sprite Get(int radius)
        {
            if (Cache.TryGetValue(radius, out Sprite cached))
            {
                return cached;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = radius;
            float rSq = r * r;
            float cornerCenter = size - r - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = true;
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    if (px >= cornerCenter && py >= cornerCenter)
                    {
                        float dx = px - cornerCenter;
                        float dy = py - cornerCenter;
                        inside = dx * dx + dy * dy <= rSq;
                    }
                    else if (px <= r + 0.5f && py >= cornerCenter)
                    {
                        float dx = px - (r + 0.5f);
                        float dy = py - cornerCenter;
                        inside = dx * dx + dy * dy <= rSq;
                    }
                    else if (px >= cornerCenter && py <= r + 0.5f)
                    {
                        float dx = px - cornerCenter;
                        float dy = py - (r + 0.5f);
                        inside = dx * dx + dy * dy <= rSq;
                    }
                    else if (px <= r + 0.5f && py <= r + 0.5f)
                    {
                        float dx = px - (r + 0.5f);
                        float dy = py - (r + 0.5f);
                        inside = dx * dx + dy * dy <= rSq;
                    }

                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(r + 1f, r + 1f, r + 1f, r + 1f));
            Cache[radius] = sprite;
            return sprite;
        }

        /// <summary>把 Image 设为圆角（9-slice），失败返回 false。</summary>
        public static bool Apply(Image image, int radius)
        {
            if (image == null)
            {
                return false;
            }

            image.sprite = Get(radius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            return true;
        }
    }
}
