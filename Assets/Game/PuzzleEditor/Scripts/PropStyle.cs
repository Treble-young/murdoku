using UnityEngine;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 一种道具：颜色 + 编号 + 圆形图标（后续可替换为素材）。
    /// </summary>
    public sealed class PropDefinition
    {
        /// <summary>在 PropStyleFactory.All 中的索引（0 起）。</summary>
        public int Index;

        /// <summary>素材文件名预留（后续导入素材时用于加载，如 prop_01.png）。</summary>
        public string Key;

        /// <summary>显示名（如"道具 1"）。</summary>
        public string DisplayName;

        /// <summary>圆形图标颜色。</summary>
        public Color Color;

        /// <summary>圆形图标 Sprite（程序生成；后续可替换素材）。</summary>
        public Sprite Sprite;

        /// <summary>卡片编号（1 起，显示在圆形中心）。</summary>
        public int Number => Index + 1;
    }

    /// <summary>
    /// 道具定义库（16 种测试道具，4×4 排布）与圆形图标纹理生成。
    /// </summary>
    public static class PropStyleFactory
    {
        private const int TexSize = 64;
        private const int PropCount = 16;

        private static PropDefinition[] all;

        public static PropDefinition[] All
        {
            get
            {
                if (all == null)
                {
                    BuildAll();
                }

                return all;
            }
        }

        private static void BuildAll()
        {
            all = new PropDefinition[PropCount];
            for (int index = 0; index < PropCount; index++)
            {
                // 16 种颜色沿色环均匀分布（鲜艳、彼此易区分）。
                Color color = Color.HSVToRGB(index / (float)PropCount, 0.68f, 0.88f);
                all[index] = new PropDefinition
                {
                    Index = index,
                    Key = $"prop_{index + 1:00}",
                    DisplayName = $"道具 {index + 1}",
                    Color = color
                };
            }
        }

        /// <summary>
        /// 为所有定义确定 Sprite 并记录索引（重复调用安全：已生成的不再重建）。
        /// 优先加载 Resources/Props/ 下的同名素材（prop_01.png ~ prop_16.png）；
        /// 缺失时回退程序生成圆形图标。
        /// </summary>
        public static void EnsureSprites()
        {
            foreach (PropDefinition def in All)
            {
                if (def.Sprite == null)
                {
                    def.Sprite = LoadSpriteOrFallback(def);
                }
            }
        }

        private static Sprite LoadSpriteOrFallback(PropDefinition def)
        {
            // 1. 直接加载 Sprite（导入类型已设为 Sprite 时）。
            Sprite sprite = TryLoadSprite(def.Key);
            if (sprite != null)
            {
                return sprite;
            }

            // 2. 兜底：加载为 Texture2D 再包一层 Sprite（图片未设置导入类型也能用）。
            if (!string.IsNullOrEmpty(def.Key))
            {
                Texture2D texture = Resources.Load<Texture2D>($"Props/{def.Key}");
                if (texture != null)
                {
                    return Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            // 3. 都没有素材：回退程序生成圆形图标。
            return CreateCircleSprite(def.Color);
        }

        private static Sprite TryLoadSprite(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return Resources.Load<Sprite>($"Props/{key}");
        }

        /// <summary>
        /// 生成一个实心圆形 Sprite（带深色描边，中心留空给编号文字显示）。
        /// </summary>
        private static Sprite CreateCircleSprite(Color color)
        {
            Texture2D texture = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[TexSize * TexSize];
            float radius = TexSize / 2f - 2f;   // 圆半径（留 2px 边缘）
            float stroke = 2.5f;                 // 描边宽度
            Color strokeColor = Color.Lerp(color, Color.black, 0.45f);

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float dx = x - TexSize / 2f + 0.5f;
                    float dy = y - TexSize / 2f + 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    Color c = Color.clear;
                    if (distance <= radius)
                    {
                        c = color;
                        if (distance > radius - stroke)
                        {
                            c = strokeColor; // 描边
                        }
                    }

                    pixels[y * TexSize + x] = c;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.name = "PropCircle";
            return Sprite.Create(texture, new Rect(0f, 0f, TexSize, TexSize), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
