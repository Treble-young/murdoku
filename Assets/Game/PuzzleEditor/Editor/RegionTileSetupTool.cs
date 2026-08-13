using System.IO;
using UnityEditor;
using UnityEngine;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 地块 / 道具素材导入工具：
    /// 1. 确保 Resources/RegionTiles/（地块）与 Resources/Props/（道具）目录存在；
    /// 2. 把目录下所有 PNG 的导入类型设为 Sprite（Single、无压缩、关 mipmap），保证颜色准确、显示清晰；
    /// 3. 打印命名对照表，方便用户按文件名放置素材。
    /// 素材缺失的地块/道具运行时自动回退程序生成图案/圆形图标。
    /// </summary>
    public static class RegionTileSetupTool
    {
        private const string TileFolder = "Assets/Game/PuzzleEditor/Resources/RegionTiles";
        private const string PropFolder = "Assets/Game/PuzzleEditor/Resources/Props";

        [MenuItem("Tools/Murdoku/Setup Region Tile Textures")]
        public static void SetupTileTextures()
        {
            SetupFolder(TileFolder, "[RegionTiles]");
            Debug.Log(GetTileNamingGuide());
        }

        [MenuItem("Tools/Murdoku/Setup Prop Textures")]
        public static void SetupPropTextures()
        {
            SetupFolder(PropFolder, "[Props]");
            Debug.Log(GetPropNamingGuide());
        }

        /// <summary>确保目录存在，并把目录下所有 PNG 的导入类型设为 Sprite（Single/无压缩/关 mipmap）。</summary>
        private static void SetupFolder(string folder, string logPrefix)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            string[] files = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
            int changed = 0;
            foreach (string file in files)
            {
                TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    dirty = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            Debug.Log($"{logPrefix} 已设置 {changed} 张素材的导入类型（Sprite/无压缩）。");
        }

        /// <summary>生成 15 个地块的命名对照说明（素材文件名 = SpriteKey.png）。</summary>
        private static string GetTileNamingGuide()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[RegionTiles] 地块素材命名对照表（图片放到 " + TileFolder + "/，文件名如下，PNG 格式）：");
            foreach (RegionDefinition def in RegionStyleFactory.All)
            {
                sb.AppendLine($"  {def.SpriteKey}.png  ← {def.DisplayName}");
            }

            return sb.ToString();
        }

        /// <summary>生成 16 个道具的命名对照说明（素材文件名 = Key.png）。</summary>
        private static string GetPropNamingGuide()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Props] 道具素材命名对照表（图片放到 " + PropFolder + "/，文件名如下，PNG 格式）：");
            foreach (PropDefinition def in PropStyleFactory.All)
            {
                sb.AppendLine($"  {def.Key}.png  ← {def.DisplayName}");
            }

            return sb.ToString();
        }
    }
}
