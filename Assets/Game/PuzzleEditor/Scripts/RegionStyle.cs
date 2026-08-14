using UnityEngine;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 一种地块：编号 + 颜色 + 占位方形（可替换为素材）。
    /// </summary>
    public sealed class RegionDefinition
    {
        /// <summary>在 RegionStyleFactory.All 中的索引（0 起）。</summary>
        public int Index;

        /// <summary>素材文件名（不含扩展名，Resources/RegionTiles/ 下的同名图片优先于占位方形）。</summary>
        public string SpriteKey;

        /// <summary>显示名（如"地块 1"）。</summary>
        public string DisplayName;

        /// <summary>占位方形颜色。</summary>
        public Color BaseColor;

        /// <summary>方形占位/素材 Sprite。</summary>
        public Sprite Sprite;

        /// <summary>卡片编号（1 起）。</summary>
        public int Number => Index + 1;
    }

    /// <summary>
    /// 地块定义库（20 种地块，5 列 × 4 行排布）。
    /// 占位显示为纯色方形（与道具面板的圆形占位逻辑一致，命名同样为「地块 + 编号」）；
    /// Resources/RegionTiles/ 下放入同名素材后自动替换为素材图案。
    /// </summary>
    public static class RegionStyleFactory
    {
        private const int TexSize = 64;
        private const int RegionCount = 20;

        private static RegionDefinition[] all;

        public static RegionDefinition[] All
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
            all = new RegionDefinition[RegionCount];
            for (int index = 0; index < RegionCount; index++)
            {
                // 20 种颜色沿色环均匀分布（柔和地面色调，彼此易区分）。
                Color color = Color.HSVToRGB(index / (float)RegionCount, 0.62f, 0.88f);
                all[index] = new RegionDefinition
                {
                    Index = index,
                    SpriteKey = $"tile_{index + 1:00}",
                    DisplayName = $"地块 {index + 1}",
                    BaseColor = color
                };
            }
        }

        /// <summary>
        /// 为所有定义确定 Sprite 并记录索引（重复调用安全：已生成的不再重建）。
        /// 优先加载 Resources/RegionTiles/ 下的同名素材；缺失时回退纯色方形占位。
        /// </summary>
        public static void EnsureSprites()
        {
            for (int index = 0; index < All.Length; index++)
            {
                RegionDefinition def = All[index];
                def.Index = index;
                if (def.Sprite == null)
                {
                    def.Sprite = LoadSpriteOrFallback(def);
                }
            }
        }

        private static Sprite LoadSpriteOrFallback(RegionDefinition def)
        {
            // 1. 直接加载 Sprite（导入类型已设为 Sprite 时）。
            Sprite sprite = TryLoadSprite(def.SpriteKey);
            if (sprite != null)
            {
                return sprite;
            }

            // 2. 兜底：加载为 Texture2D 再包一层 Sprite（图片未设置导入类型也能用）。
            if (!string.IsNullOrEmpty(def.SpriteKey))
            {
                Texture2D texture = Resources.Load<Texture2D>($"RegionTiles/{def.SpriteKey}");
                if (texture != null)
                {
                    return Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            // 3. 都没有素材：回退纯色方形占位。
            return CreateSprite(def);
        }

        private static Sprite TryLoadSprite(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return Resources.Load<Sprite>($"RegionTiles/{key}");
        }

        /// <summary>
        /// 生成占位方形 Sprite（纯色 + 细深色描边，风格与道具圆形占位一致）。
        /// </summary>
        public static Sprite CreateSprite(RegionDefinition def)
        {
            Texture2D texture = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[TexSize * TexSize];
            Color baseColor = def.BaseColor;
            Color edgeColor = Color.Lerp(baseColor, Color.black, 0.40f);
            const int stroke = 2;

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    bool onEdge = x < stroke || y < stroke || x >= TexSize - stroke || y >= TexSize - stroke;
                    pixels[y * TexSize + x] = onEdge ? edgeColor : baseColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.name = def.DisplayName;
            return Sprite.Create(texture, new Rect(0f, 0f, TexSize, TexSize), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
