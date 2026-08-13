using UnityEngine;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 地块样式枚举：图案不同，颜色可变体。
    /// </summary>
    public enum RegionStyle
    {
        Checkered = 0, // 方格地砖
        Wood = 1,      // 木地板
        Sand = 2,      // 沙滩
        Grass = 3,     // 草坪
        Water = 4,     // 水域
    }

    /// <summary>
    /// 一种地块：样式 + 颜色变体（图案相同，仅颜色不同）。
    /// </summary>
    public sealed class RegionDefinition
    {
        public string StyleName;
        public string VariantName;
        public Color BaseColor;
        public RegionStyle Style;
        public Sprite Sprite;

        /// <summary>
        /// 素材文件名（不含扩展名，Resources/RegionTiles/ 下的同名图片优先于程序生成图案）。
        /// </summary>
        public string SpriteKey;

        /// <summary>在 RegionStyleFactory.All 中的索引（用于关卡序列化保存）。</summary>
        public int Index;

        public string DisplayName => $"{StyleName} {VariantName}";
    }

    /// <summary>
    /// 地块定义库（5 样式 × 3 颜色 = 15 种）与程序化图案纹理生成。
    /// </summary>
    public static class RegionStyleFactory
    {
        private const int TexSize = 64;

        public static readonly RegionDefinition[] All =
        {
            // 方格地砖
            Def("方格地砖", "灰白", RegionStyle.Checkered, new Color(0.80f, 0.82f, 0.85f, 1f), "checkered_light"),
            Def("方格地砖", "米黄", RegionStyle.Checkered, new Color(0.88f, 0.81f, 0.63f, 1f), "checkered_beige"),
            Def("方格地砖", "蓝灰", RegionStyle.Checkered, new Color(0.66f, 0.75f, 0.83f, 1f), "checkered_slate"),
            // 木地板
            Def("木地板", "浅棕", RegionStyle.Wood, new Color(0.77f, 0.59f, 0.39f, 1f), "wood_light"),
            Def("木地板", "中棕", RegionStyle.Wood, new Color(0.63f, 0.45f, 0.28f, 1f), "wood_medium"),
            Def("木地板", "红棕", RegionStyle.Wood, new Color(0.55f, 0.35f, 0.23f, 1f), "wood_dark"),
            // 沙滩
            Def("沙滩", "浅黄", RegionStyle.Sand, new Color(0.93f, 0.87f, 0.65f, 1f), "sand_light"),
            Def("沙滩", "金黄", RegionStyle.Sand, new Color(0.88f, 0.78f, 0.46f, 1f), "sand_gold"),
            Def("沙滩", "沙橙", RegionStyle.Sand, new Color(0.87f, 0.72f, 0.49f, 1f), "sand_orange"),
            // 草坪
            Def("草坪", "浅绿", RegionStyle.Grass, new Color(0.56f, 0.79f, 0.43f, 1f), "grass_light"),
            Def("草坪", "草绿", RegionStyle.Grass, new Color(0.38f, 0.68f, 0.30f, 1f), "grass_mid"),
            Def("草坪", "深绿", RegionStyle.Grass, new Color(0.26f, 0.56f, 0.24f, 1f), "grass_dark"),
            // 水域
            Def("水域", "浅蓝", RegionStyle.Water, new Color(0.43f, 0.68f, 0.88f, 1f), "water_light"),
            Def("水域", "天蓝", RegionStyle.Water, new Color(0.25f, 0.55f, 0.85f, 1f), "water_sky"),
            Def("水域", "深蓝", RegionStyle.Water, new Color(0.14f, 0.38f, 0.72f, 1f), "water_deep"),
        };

        /// <summary>
        /// 为所有定义确定 Sprite 并记录索引（重复调用安全：已生成的不再重建）。
        /// 优先加载 Resources/RegionTiles/ 下的同名素材；缺失时回退程序生成图案。
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

            // 3. 都没有素材：回退程序生成图案。
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
        /// 生成地块图案 Sprite（图案由样式决定，颜色由 BaseColor 决定）。
        /// </summary>
        public static Sprite CreateSprite(RegionDefinition def)
        {
            Texture2D texture = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[TexSize * TexSize];

            switch (def.Style)
            {
                case RegionStyle.Checkered:
                    FillCheckered(pixels, def.BaseColor);
                    break;
                case RegionStyle.Wood:
                    FillWood(pixels, def.BaseColor);
                    break;
                case RegionStyle.Sand:
                    FillSand(pixels, def.BaseColor);
                    break;
                case RegionStyle.Grass:
                    FillGrass(pixels, def.BaseColor);
                    break;
                case RegionStyle.Water:
                    FillWater(pixels, def.BaseColor);
                    break;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.name = def.DisplayName;
            return Sprite.Create(texture, new Rect(0f, 0f, TexSize, TexSize), new Vector2(0.5f, 0.5f), 64f);
        }

        private static RegionDefinition Def(string style, string variant, RegionStyle s, Color c, string spriteKey)
        {
            return new RegionDefinition
            {
                StyleName = style,
                VariantName = variant,
                Style = s,
                BaseColor = c,
                SpriteKey = spriteKey
            };
        }

        private static Color Lighten(Color c, float t)
        {
            return Color.Lerp(c, Color.white, t);
        }

        private static Color Darken(Color c, float t)
        {
            return Color.Lerp(c, Color.black, t);
        }

        private static int Px(int x, int y)
        {
            return y * TexSize + x;
        }

        /// <summary>画一个小圆点石子。</summary>
        private static void DrawStone(Color[] p, int cx, int cy, int radius, Color color)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius)
                    {
                        continue;
                    }

                    int x = cx + dx;
                    int y = cy + dy;
                    if (x >= 0 && x < TexSize && y >= 0 && y < TexSize)
                    {
                        p[Px(x, y)] = color;
                    }
                }
            }
        }

        /// <summary>方格地砖：砌砖样式（砖块错位 + 深色砖缝，参考 Tilemap 砖块）。</summary>
        private static void FillCheckered(Color[] p, Color baseColor)
        {
            const int brickW = 16;
            const int brickH = 8;
            Color seam = Darken(baseColor, 0.35f);
            Color brickLight = Lighten(baseColor, 0.08f);
            Color brickDark = Darken(baseColor, 0.06f);

            for (int y = 0; y < TexSize; y++)
            {
                int row = y / brickH;
                int offset = row % 2 == 0 ? 0 : brickW / 2;
                bool seamY = y % brickH < 2;

                for (int x = 0; x < TexSize; x++)
                {
                    int bx = (x + offset) % brickW;
                    bool seamX = bx < 2;
                    Color c = ((x + offset) / brickW + row) % 2 == 0 ? brickLight : brickDark;
                    if (seamY || seamX)
                    {
                        c = seam;
                    }

                    p[Px(x, y)] = c;
                }
            }
        }

        /// <summary>木地板：横向板条 + 明显板缝 + 竖木纹（参考 Tilemap 木板）。</summary>
        private static void FillWood(Color[] p, Color baseColor)
        {
            const int plank = 14;
            Color seam = Darken(baseColor, 0.42f);

            for (int y = 0; y < TexSize; y++)
            {
                int plankIndex = y / plank;
                bool seamLine = y % plank < 2;
                Color plankColor = plankIndex % 2 == 0
                    ? Lighten(baseColor, 0.07f)
                    : Darken(baseColor, 0.06f);

                for (int x = 0; x < TexSize; x++)
                {
                    Color c = plankColor;
                    // 竖木纹：每 10px 一组明暗条。
                    if (x % 10 < 2)
                    {
                        c = Darken(c, 0.10f);
                    }
                    else if (x % 10 >= 7)
                    {
                        c = Lighten(c, 0.06f);
                    }

                    if (seamLine)
                    {
                        c = seam;
                    }

                    p[Px(x, y)] = c;
                }
            }
        }

        /// <summary>沙滩：米黄底 + 噪点 + 小石子（参考 Tilemap 沙滩）。</summary>
        private static void FillSand(Color[] p, Color baseColor)
        {
            System.Random rng = new System.Random(12345);
            for (int i = 0; i < p.Length; i++)
            {
                double n = rng.NextDouble();
                Color c = baseColor;
                if (n < 0.05)
                {
                    c = Lighten(baseColor, 0.16f);
                }
                else if (n < 0.11)
                {
                    c = Darken(baseColor, 0.10f);
                }

                p[i] = c;
            }

            // 散落的小石子。
            for (int i = 0; i < 6; i++)
            {
                int cx = 4 + rng.Next(TexSize - 8);
                int cy = 4 + rng.Next(TexSize - 8);
                DrawStone(p, cx, cy, 2, Darken(baseColor, 0.22f));
            }
        }

        /// <summary>草坪：绿底 + 草点草簇 + 小石子（参考 Tilemap 草地）。</summary>
        private static void FillGrass(Color[] p, Color baseColor)
        {
            System.Random rng = new System.Random(777);
            Color grassDark = Darken(baseColor, 0.24f);
            Color grassLight = Lighten(baseColor, 0.15f);

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    Color c = baseColor;
                    if (x % 8 == 4 && y % 6 < 3)
                    {
                        c = grassDark; // 草簇
                    }
                    else if (rng.NextDouble() < 0.06)
                    {
                        c = grassDark; // 草点
                    }
                    else if (rng.NextDouble() < 0.05)
                    {
                        c = grassLight;
                    }

                    p[Px(x, y)] = c;
                }
            }

            // 小石子。
            for (int i = 0; i < 5; i++)
            {
                int cx = 4 + rng.Next(TexSize - 8);
                int cy = 4 + rng.Next(TexSize - 8);
                DrawStone(p, cx, cy, 2, Darken(baseColor, 0.30f));
            }
        }

        /// <summary>水域：蓝底 + 弧形正弦波纹（参考 Tilemap 水纹）。</summary>
        private static void FillWater(Color[] p, Color baseColor)
        {
            Color waveLight = Lighten(baseColor, 0.22f);
            Color waveDark = Darken(baseColor, 0.12f);

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    p[Px(x, y)] = baseColor;
                }
            }

            // 弧形波纹：每 14px 一道正弦波，亮/暗交替。
            for (int band = 0; band < 5; band++)
            {
                int baseY = band * 14 + 3;
                Color waveColor = band % 2 == 0 ? waveLight : waveDark;
                for (int x = 0; x < TexSize; x++)
                {
                    int py = baseY + (int)(Mathf.Sin(x / 6f + band * 1.7f) * 3f);
                    if (py >= 0 && py < TexSize)
                    {
                        p[Px(x, py)] = waveColor;
                        if (py + 1 < TexSize)
                        {
                            p[Px(x, py + 1)] = waveColor;
                        }
                    }
                }
            }
        }
    }
}
