using System.IO;
using UnityEditor;
using UnityEngine;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 地块素材导入工具：
    /// 1. 确保 Resources/RegionTiles/ 目录存在；
    /// 2. 把目录下所有 PNG 的导入类型设为 Sprite（Single、无压缩、关 mipmap），保证颜色准确、显示清晰；
    /// 3. 打印 15 个地块的命名对照表，方便用户按文件名放置素材。
    /// 素材缺失的地块运行时自动回退程序生成图案。
    /// </summary>
    public static class RegionTileSetupTool
    {
        private const string TileFolder = "Assets/Game/PuzzleEditor/Resources/RegionTiles";

        [MenuItem("Tools/Murdoku/Setup Region Tile Textures")]
        public static void SetupTileTextures()
        {
            if (!Directory.Exists(TileFolder))
            {
                Directory.CreateDirectory(TileFolder);
                AssetDatabase.Refresh();
            }

            string[] files = Directory.GetFiles(TileFolder, "*.png", SearchOption.TopDirectoryOnly);
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

            Debug.Log($"[RegionTiles] 已设置 {changed} 张素材的导入类型（Sprite/无压缩）。");
            Debug.Log(GetNamingGuide());
        }

        /// <summary>生成 15 个地块的命名对照说明（素材文件名 = SpriteKey.png）。</summary>
        private static string GetNamingGuide()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[RegionTiles] 素材命名对照表（图片放到 " + TileFolder + "/，文件名如下，PNG 格式）：");
            foreach (RegionDefinition def in RegionStyleFactory.All)
            {
                sb.AppendLine($"  {def.SpriteKey}.png  ← {def.DisplayName}");
            }

            return sb.ToString();
        }
    }
}
