using System.Collections.Generic;
using Murdoku.Audio;
using Murdoku.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 谜题编辑器的「墙壁/区域」编辑控制器：
    /// - 管理模式切换（放置 / 编辑墙壁）；
    /// - 编辑墙壁模式下生成可点击的边界线（粗线=墙、细线=无墙），点击切换；
    /// - 每次墙变化后自动计算连通区域，并用不同底色为格子着色。
    /// 参考 Murdoku Playground 的 Edit walls 交互。
    /// </summary>
    public sealed class WallEditController : MonoBehaviour
    {
        public enum EditorMode
        {
            Place,
            EditWalls
        }

        private static readonly Color[] RegionColors =
        {
            new Color(0.98f, 0.95f, 0.88f, 1f), // 淡黄
            new Color(0.89f, 0.94f, 0.85f, 1f), // 淡绿
            new Color(0.97f, 0.89f, 0.91f, 1f), // 淡粉
            new Color(0.86f, 0.92f, 0.97f, 1f), // 淡蓝
            new Color(0.91f, 0.89f, 0.96f, 1f), // 淡紫
            new Color(0.97f, 0.92f, 0.85f, 1f), // 淡橙
            new Color(0.87f, 0.94f, 0.93f, 1f), // 淡青
            new Color(0.92f, 0.92f, 0.94f, 1f)  // 淡灰
        };

        private const float BorderHitThickness = 14f;
        private const float WallThickness = 10f;
        private const float OpenThickness = 2f;
        private const float FrameThickness = 4f;
        private const float HoverOpenThickness = 4f;

        private static readonly Color WallColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color OpenColor = new Color(0.45f, 0.50f, 0.58f, 0.55f);
        private static readonly Color FrameColor = new Color(0.62f, 0.70f, 0.85f, 1f);
        private static readonly Color HoverColor = new Color(1f, 0.62f, 0.22f, 1f);

        [SerializeField] private PuzzleBoardController board;

        private readonly List<BorderButton> borders = new List<BorderButton>();
        private readonly List<Image> frames = new List<Image>();
        private readonly HashSet<int> hoveredBorders = new HashSet<int>();
        private WallMap walls;
        private EditorMode mode = EditorMode.Place;
        private int currentSize;
        private RectTransform overlayRoot;

        // 区域命名：名字数据（按区域 id 索引）、名字标签（名字由协调器的「区域命名」面板编辑）。
        private readonly List<string> regionNames = new List<string>();
        private readonly List<Vector2> regionNameOffsets = new List<Vector2>();
        private readonly List<GameObject> regionLabels = new List<GameObject>();

        private bool[] pendingHorizontalWalls;
        private bool[] pendingVerticalWalls;
        private bool hasPendingWallState;

        public EditorMode Mode => mode;

        public WallMap Walls => walls;

        public event System.Action<EditorMode> ModeChanged;

        /// <summary>
        /// 用存档中的墙数据重建棋盘墙体（水平墙/垂直墙均为行优先展平的一维数组）。
        /// 墙状态会挂起，后续因布局重建触发的 RebuildWalls 会继续沿用，
        /// 直到玩家手动编辑墙体或调用 ClearPendingWallState。
        /// </summary>
        public void ApplyWallState(int size, bool[] horizontalWalls, bool[] verticalWalls)
        {
            pendingHorizontalWalls = horizontalWalls;
            pendingVerticalWalls = verticalWalls;
            hasPendingWallState = true;
            RebuildWalls(size, size);
        }

        public void ClearPendingWallState()
        {
            pendingHorizontalWalls = null;
            pendingVerticalWalls = null;
            hasPendingWallState = false;
        }

        /// <summary>区域名字列表（按区域 id 索引，空字符串 = 未命名），供保存关卡使用。</summary>
        public IReadOnlyList<string> RegionNames => regionNames;

        /// <summary>区域名字文字偏移列表（按区域 id 索引，相对几何中心的像素偏移），供保存关卡使用。</summary>
        public IReadOnlyList<Vector2> RegionNameOffsets => regionNameOffsets;

        /// <summary>
        /// 用存档中的区域名字恢复显示（载入关卡时调用；旧存档无字段自动跳过）。
        /// </summary>
        public void ApplyRegionNames(IReadOnlyList<string> names)
        {
            regionNames.Clear();
            if (names != null)
            {
                regionNames.AddRange(names);
            }

            UpdateRegionLabels();
        }

        /// <summary>
        /// 用存档中的区域名字偏移恢复显示（载入关卡时调用；旧存档无字段自动跳过）。
        /// </summary>
        public void ApplyRegionNameOffsets(IReadOnlyList<Vector2> offsets)
        {
            regionNameOffsets.Clear();
            if (offsets != null)
            {
                regionNameOffsets.AddRange(offsets);
            }

            UpdateRegionLabels();
        }

        /// <summary>设置某区域的名字（空字符串 = 清除名字），供协调器的区域命名面板调用。</summary>
        public void SetRegionName(int regionId, string name)
        {
            string trimmed = name == null ? string.Empty : name.Trim();
            while (regionNames.Count <= regionId)
            {
                regionNames.Add(string.Empty);
            }

            regionNames[regionId] = trimmed;
            UpdateRegionLabels();
        }

        /// <summary>设置某区域名字文字的偏移（相对几何中心的像素偏移），供协调器的区域命名面板调用。</summary>
        public void SetRegionNameOffset(int regionId, Vector2 offset)
        {
            while (regionNameOffsets.Count <= regionId)
            {
                regionNameOffsets.Add(Vector2.zero);
            }

            regionNameOffsets[regionId] = offset;
            UpdateRegionLabels();
        }

        private struct BorderButton
        {
            public bool IsHorizontal;
            public int Row;
            public int Col;
            public Button Button;
            public Image Visual;
        }

        private void OnEnable()
        {
            if (board != null)
            {
                board.GridGenerated -= HandleGridGenerated;
                board.GridGenerated += HandleGridGenerated;
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.GridGenerated -= HandleGridGenerated;
            }
        }

        private void Start()
        {
            StartCoroutine(RebuildAfterLayout());
        }

        private System.Collections.IEnumerator RebuildAfterLayout()
        {
            // 等待一帧让 Canvas 完成布局，确保 GridRoot.rect 有效后再计算边界线位置。
            yield return null;
            RebuildForCurrentBoard();
        }

        public void SetBoard(PuzzleBoardController controller)
        {
            if (board == controller)
            {
                return;
            }

            if (board != null)
            {
                board.GridGenerated -= HandleGridGenerated;
            }

            board = controller;

            if (board != null)
            {
                board.GridGenerated -= HandleGridGenerated;
                board.GridGenerated += HandleGridGenerated;
            }

            RebuildForCurrentBoard();
        }

        public void SetMode(EditorMode newMode)
        {
            if (mode == newMode)
            {
                return;
            }

            mode = newMode;
            ApplyMode();
            ModeChanged?.Invoke(mode);
        }

        private void HandleGridGenerated(int rows, int columns)
        {
            // 棋盘重建后需等一帧，等 GridLayoutGroup 与 rect 更新再重算边界线位置。
            StartCoroutine(RebuildAfterLayoutGenerated(rows, columns));
        }

        private System.Collections.IEnumerator RebuildAfterLayoutGenerated(int rows, int columns)
        {
            yield return null;
            RebuildWalls(rows, columns);
        }

        private void RebuildForCurrentBoard()
        {
            if (board == null)
            {
                return;
            }

            int size = Mathf.Max(board.Rows, board.Columns);
            if (size == board.Rows && size == board.Columns)
            {
                RebuildWalls(size, size);
            }
        }

        private void RebuildWalls(int rows, int columns)
        {
            ClearBorders();
            currentSize = rows;
            walls = new WallMap(rows);
            bool appliedPending = hasPendingWallState;
            if (appliedPending)
            {
                ApplyPendingWallState();
            }

            CreateBorders();
            ApplyMode();

            if (appliedPending)
            {
                UpdateAllBorderVisuals();
                RecolorRegions();
            }
        }

        private void ApplyPendingWallState()
        {
            if (walls == null)
            {
                return;
            }

            int size = walls.Size;
            if (pendingHorizontalWalls != null)
            {
                int index = 0;
                for (int row = 0; row < size - 1; row++)
                {
                    for (int col = 0; col < size; col++)
                    {
                        if (index < pendingHorizontalWalls.Length)
                        {
                            walls.SetHorizontalWall(row, col, pendingHorizontalWalls[index]);
                        }

                        index++;
                    }
                }
            }

            if (pendingVerticalWalls != null)
            {
                int index = 0;
                for (int row = 0; row < size; row++)
                {
                    for (int col = 0; col < size - 1; col++)
                    {
                        if (index < pendingVerticalWalls.Length)
                        {
                            walls.SetVerticalWall(row, col, pendingVerticalWalls[index]);
                        }

                        index++;
                    }
                }
            }
        }

        private void ClearBorders()
        {
            foreach (BorderButton border in borders)
            {
                if (border.Button != null)
                {
                    Destroy(border.Button.gameObject);
                }
            }

            borders.Clear();

            foreach (Image frame in frames)
            {
                if (frame != null)
                {
                    Destroy(frame.gameObject);
                }
            }

            frames.Clear();

            // 棋盘重建：区域结构完全变化，清理区域名字与标签。
            foreach (GameObject label in regionLabels)
            {
                if (label != null)
                {
                    Destroy(label);
                }
            }

            regionLabels.Clear();
            regionNames.Clear();
            regionNameOffsets.Clear();
        }

        private void CreateBorders()
        {
            if (board == null || board.GridRoot == null || walls == null)
            {
                return;
            }

            GridLayoutGroup layout = board.GridRoot.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                return;
            }

            // 边界线必须放在独立覆盖层（脱离 GridLayoutGroup 的强制重排），否则会被当成格子排列。
            EnsureOverlay();
            if (overlayRoot == null)
            {
                return;
            }

            float cell = layout.cellSize.x;
            float spacing = layout.spacing.x;
            // 行列数必须与墙数据（walls）完全一致，否则查墙状态/算位置会数组越界。
            // 不能用 layout.constraintCount：棋盘重建交错的瞬间布局列数可能滞后于墙尺寸。
            int rows = walls.Size;
            int columns = walls.Size;
            float gridWidth = overlayRoot.rect.width;
            float gridHeight = overlayRoot.rect.height;

            float totalWidth = columns * cell + (columns - 1) * spacing;
            float totalHeight = rows * cell + (rows - 1) * spacing;
            float originX = (gridWidth - totalWidth) * 0.5f;
            float originY = (gridHeight - totalHeight) * 0.5f;

            // 内部边界线：水平 (rows-1)×columns，垂直 rows×(columns-1)
            for (int row = 0; row < rows - 1; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    float x = originX + col * (cell + spacing) + (cell + spacing) * 0.5f;
                    float y = originY + row * (cell + spacing) + cell + spacing * 0.5f;
                    CreateBorder(true, row, col, x, y, cell + spacing, BorderHitThickness);
                }
            }

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns - 1; col++)
                {
                    float x = originX + col * (cell + spacing) + cell + spacing * 0.5f;
                    float y = originY + row * (cell + spacing) + (cell + spacing) * 0.5f;
                    CreateBorder(false, row, col, x, y, BorderHitThickness, cell + spacing);
                }
            }

            // 外框（不可点击）
            CreateFrame(originX + totalWidth * 0.5f, originY - spacing * 0.5f, totalWidth + spacing, FrameThickness);
            CreateFrame(originX + totalWidth * 0.5f, originY + totalHeight + spacing * 0.5f, totalWidth + spacing, FrameThickness);
            CreateFrame(originX - spacing * 0.5f, originY + totalHeight * 0.5f, FrameThickness, totalHeight + spacing);
            CreateFrame(originX + totalWidth + spacing * 0.5f, originY + totalHeight * 0.5f, FrameThickness, totalHeight + spacing);
        }

        /// <summary>
        /// 创建与棋盘完全重叠的独立覆盖层，用于承载边界线与外框。
        /// 覆盖层是 board.GridRoot 的兄弟节点（同一父级），不受 GridLayoutGroup 的强制重排影响。
        /// </summary>
        private void EnsureOverlay()
        {
            if (overlayRoot != null)
            {
                return;
            }

            if (board == null || board.GridRoot == null)
            {
                return;
            }

            RectTransform gridRoot = board.GridRoot;
            GameObject overlay = new GameObject("WallOverlay", typeof(RectTransform));
            overlay.layer = LayerMask.NameToLayer("UI");
            overlayRoot = overlay.GetComponent<RectTransform>();
            overlayRoot.SetParent(gridRoot.parent, false);

            // 与棋盘精确重叠：复制锚点、轴心与位置尺寸。
            overlayRoot.anchorMin = gridRoot.anchorMin;
            overlayRoot.anchorMax = gridRoot.anchorMax;
            overlayRoot.pivot = gridRoot.pivot;
            overlayRoot.anchoredPosition = gridRoot.anchoredPosition;
            overlayRoot.sizeDelta = gridRoot.sizeDelta;

            // 渲染在棋盘上层：放到兄弟节点末尾。
            overlayRoot.SetAsLastSibling();
        }

        private void CreateBorder(bool isHorizontal, int row, int col, float x, float y, float width, float height)
        {
            GameObject borderObject = new GameObject(
                isHorizontal ? $"HWall_{row}_{col}" : $"VWall_{row}_{col}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            borderObject.layer = LayerMask.NameToLayer("UI");
            RectTransform borderRect = borderObject.GetComponent<RectTransform>();
            borderRect.SetParent(overlayRoot, false);
            PlaceRect(borderRect, x, y, width, height);

            Image hitImage = borderObject.GetComponent<Image>();
            hitImage.color = Color.clear;
            hitImage.raycastTarget = true;

            Button button = borderObject.GetComponent<Button>();
            button.targetGraphic = hitImage;
            button.transition = Selectable.Transition.None;

            GameObject visualObject = new GameObject("Visual", typeof(RectTransform), typeof(Image));
            RectTransform visualRect = visualObject.GetComponent<RectTransform>();
            visualRect.SetParent(borderRect, false);
            // 中心锚点 + sizeDelta 控制粗细（不能用 stretch，否则尺寸设置无效）。
            visualRect.anchorMin = Vector2.one * 0.5f;
            visualRect.anchorMax = Vector2.one * 0.5f;
            visualRect.pivot = Vector2.one * 0.5f;
            visualRect.anchoredPosition = Vector2.zero;
            visualRect.sizeDelta = isHorizontal
                ? new Vector2(width, OpenThickness)
                : new Vector2(OpenThickness, height);

            Image visual = visualObject.GetComponent<Image>();
            visual.raycastTarget = false;

            BorderButton border = new BorderButton
            {
                IsHorizontal = isHorizontal,
                Row = row,
                Col = col,
                Button = button,
                Visual = visual
            };
            borders.Add(border);

            int capturedIndex = borders.Count - 1;
            button.onClick.AddListener(() => HandleBorderClicked(capturedIndex));

            WallBorderButton hoverTrigger = borderObject.AddComponent<WallBorderButton>();
            hoverTrigger.BorderIndex = capturedIndex;
            hoverTrigger.HoverChanged = HandleBorderHover;
        }

        private void CreateFrame(float x, float y, float width, float height)
        {
            GameObject frameObject = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform frameRect = frameObject.GetComponent<RectTransform>();
            frameRect.SetParent(overlayRoot, false);
            PlaceRect(frameRect, x, y, width, height);

            Image frame = frameObject.GetComponent<Image>();
            frame.color = FrameColor;
            frame.raycastTarget = false;
            frames.Add(frame);
        }

        private static void PlaceRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = new Vector2(width, height);
            // y 轴：UI 坐标向下为正，anchoredPosition 向上为正，需要取反。
            rect.anchoredPosition = new Vector2(x - rect.parent.GetComponent<RectTransform>().rect.width * 0.5f, -(y - rect.parent.GetComponent<RectTransform>().rect.height * 0.5f));
        }

        private void HandleBorderClicked(int borderIndex)
        {
            if (mode != EditorMode.EditWalls || walls == null || borderIndex < 0 || borderIndex >= borders.Count)
            {
                return;
            }

            ClearPendingWallState();
            GameAudio.Play(SfxCue.UiClick);

            BorderButton border = borders[borderIndex];
            if (border.IsHorizontal)
            {
                walls.ToggleHorizontalWall(border.Row, border.Col);
            }
            else
            {
                walls.ToggleVerticalWall(border.Row, border.Col);
            }

            UpdateBorderVisual(borderIndex);
            RecolorRegions();
        }

        private void HandleBorderHover(int borderIndex, bool isHover)
        {
            if (mode != EditorMode.EditWalls || borderIndex < 0 || borderIndex >= borders.Count)
            {
                return;
            }

            if (isHover)
            {
                hoveredBorders.Add(borderIndex);
            }
            else
            {
                hoveredBorders.Remove(borderIndex);
            }

            UpdateBorderVisual(borderIndex);
        }

        private bool GetWallState(BorderButton border)
        {
            if (walls == null)
            {
                return false;
            }

            int size = walls.Size;
            if (border.IsHorizontal)
            {
                // 水平墙数组 (size-1)×size。
                if (border.Row < 0 || border.Row >= size - 1 || border.Col < 0 || border.Col >= size)
                {
                    return false;
                }

                return walls.GetHorizontalWall(border.Row, border.Col);
            }

            // 垂直墙数组 size×(size-1)。
            if (border.Row < 0 || border.Row >= size || border.Col < 0 || border.Col >= size - 1)
            {
                return false;
            }

            return walls.GetVerticalWall(border.Row, border.Col);
        }

        private void ApplyMode()
        {
            if (board == null)
            {
                return;
            }

            bool editWalls = mode == EditorMode.EditWalls;

            foreach (PuzzleBoardCellUI cell in board.Cells)
            {
                if (cell == null)
                {
                    continue;
                }

                cell.SetInteractionEnabled(!editWalls);
            }

            // 两种模式都显示已建的墙与外框；仅墙壁模式显示全部边界线（含无墙细线）。
            RefreshBorderVisibility();

            foreach (Image frame in frames)
            {
                if (frame != null)
                {
                    frame.gameObject.SetActive(true);
                }
            }

            if (editWalls)
            {
                UpdateAllBorderVisuals();
                RecolorRegions();
            }
            else
            {
                hoveredBorders.Clear();
                foreach (PuzzleBoardCellUI cell in board.Cells)
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    cell.SetRegionOverlay(null);
                }

                // 放置模式下已建的墙仍以黑色粗线显示，便于查看区域划分。
                UpdateAllBorderVisuals();
            }
        }

        /// <summary>
        /// 墙壁模式下显示全部边界线并可点击；放置模式只显示已建的墙（不可点击）。
        /// </summary>
        private void RefreshBorderVisibility()
        {
            bool editWalls = mode == EditorMode.EditWalls;
            foreach (BorderButton border in borders)
            {
                if (border.Button == null)
                {
                    continue;
                }

                bool isWall = GetWallState(border);
                border.Button.gameObject.SetActive(editWalls || isWall);
                border.Button.interactable = editWalls;
            }
        }

        private void UpdateAllBorderVisuals()
        {
            for (int index = 0; index < borders.Count; index++)
            {
                UpdateBorderVisual(index);
            }
        }

        private void UpdateBorderVisual(int borderIndex)
        {
            BorderButton border = borders[borderIndex];
            if (border.Visual == null || walls == null)
            {
                return;
            }

            bool isWall = GetWallState(border);
            bool hovered = hoveredBorders.Contains(borderIndex) && mode == EditorMode.EditWalls;

            Color color;
            float thickness;
            if (hovered)
            {
                color = HoverColor;
                thickness = isWall ? WallThickness : HoverOpenThickness;
            }
            else if (isWall)
            {
                color = WallColor;
                thickness = WallThickness;
            }
            else
            {
                color = OpenColor;
                thickness = OpenThickness;
            }

            border.Visual.color = color;
            Vector2 size = border.Visual.rectTransform.sizeDelta;
            if (border.IsHorizontal)
            {
                size.y = thickness;
            }
            else
            {
                size.x = thickness;
            }

            border.Visual.rectTransform.sizeDelta = size;
        }

        private void RecolorRegions()
        {
            if (walls == null || board == null)
            {
                return;
            }

            // 区域叠加层只在墙壁模式下显示（辅助区分区域）：
            // 游玩模式/放置模式下即使载入了墙状态，也要清除叠加层，避免棋盘出现半透明色块。
            if (mode != EditorMode.EditWalls)
            {
                foreach (PuzzleBoardCellUI cell in board.Cells)
                {
                    if (cell != null)
                    {
                        cell.SetRegionOverlay(null);
                    }
                }

                return;
            }

            int[,] regions = walls.ComputeRegions();
            int columns = board.Columns;
            for (int index = 0; index < board.Cells.Count; index++)
            {
                PuzzleBoardCellUI cell = board.Cells[index];
                if (cell == null)
                {
                    continue;
                }

                int row = index / columns;
                int col = index % columns;
                Color regionColor = RegionColors[regions[row, col] % RegionColors.Length];
                // 区域色以半透明叠加层呈现：地块图案保留可见，同时辅助区分区域。
                regionColor.a = 0.35f;
                cell.SetRegionOverlay(regionColor);
            }

            // 墙变化后更新区域名字标签的位置。
            UpdateRegionLabels();
        }

        /// <summary>
        /// 计算格子中心在覆盖层本地坐标系中的位置（与 CreateBorders 的坐标公式一致）。
        /// </summary>
        private Vector2 GetCellCenter(int row, int col)
        {
            GridLayoutGroup layout = board.GridRoot.GetComponent<GridLayoutGroup>();
            if (layout == null || overlayRoot == null)
            {
                return Vector2.zero;
            }

            float cell = layout.cellSize.x;
            float spacing = layout.spacing.x;
            // 行列数必须与墙数据（walls）完全一致，否则查墙状态/算位置会数组越界。
            // 不能用 layout.constraintCount：棋盘重建交错的瞬间布局列数可能滞后于墙尺寸。
            int rows = walls.Size;
            int columns = walls.Size;
            float gridWidth = overlayRoot.rect.width;
            float gridHeight = overlayRoot.rect.height;

            float totalWidth = columns * cell + (columns - 1) * spacing;
            float totalHeight = rows * cell + (rows - 1) * spacing;
            float originX = (gridWidth - totalWidth) * 0.5f;
            float originY = (gridHeight - totalHeight) * 0.5f;

            return new Vector2(
                originX + col * (cell + spacing) + (cell + spacing) * 0.5f,
                originY + row * (cell + spacing) + (cell + spacing) * 0.5f);
        }

        /// <summary>
        /// 重建区域名字标签：为每个已命名的区域在区域中心格子上方创建名字文本。
        /// 两种模式都显示（出题/游玩均可看到区域名）；未命名的区域不显示。
        /// </summary>
        private void UpdateRegionLabels()
        {
            foreach (GameObject label in regionLabels)
            {
                if (label != null)
                {
                    Destroy(label);
                }
            }

            regionLabels.Clear();

            if (walls == null || board == null || overlayRoot == null)
            {
                return;
            }

            int[,] regions = walls.ComputeRegions();
            int columns = board.Columns;
            int regionCount = 0;
            for (int index = 0; index < board.Cells.Count; index++)
            {
                if (regions[index / columns, index % columns] >= regionCount)
                {
                    regionCount = regions[index / columns, index % columns] + 1;
                }
            }

            for (int regionId = 0; regionId < regionCount; regionId++)
            {
                string name = regionId < regionNames.Count ? regionNames[regionId] : null;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                // 区域几何中心的格子。
                int centerIndex = FindRegionCenterCell(regions, columns, regionId);
                if (centerIndex < 0)
                {
                    continue;
                }

                int row = centerIndex / columns;
                int col = centerIndex % columns;
                Vector2 center = GetCellCenter(row, col);
                Vector2 offset = regionId < regionNameOffsets.Count ? regionNameOffsets[regionId] : Vector2.zero;

                GameObject labelRoot = CreateRegionLabel(name);
                if (labelRoot == null)
                {
                    continue;
                }

                RectTransform labelRect = labelRoot.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.one * 0.5f;
                labelRect.anchorMax = Vector2.one * 0.5f;
                labelRect.pivot = Vector2.one * 0.5f;
                labelRect.sizeDelta = new Vector2(200f, 64f);
                labelRect.anchoredPosition = new Vector2(
                    center.x - overlayRoot.rect.width * 0.5f + offset.x,
                    -(center.y - overlayRoot.rect.height * 0.5f) + offset.y);
                regionLabels.Add(labelRoot);
            }
        }

        /// <summary>找区域内距离几何中心最近的格子索引（行优先）。</summary>
        private static int FindRegionCenterCell(int[,] regions, int columns, int targetRegion)
        {
            int rows = regions.GetLength(0);
            float bestDistance = float.MaxValue;
            int bestIndex = -1;
            float centerRow = 0f;
            float centerCol = 0f;
            int count = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (regions[row, col] != targetRegion)
                    {
                        continue;
                    }

                    centerRow += row;
                    centerCol += col;
                    count++;
                }
            }

            if (count == 0)
            {
                return -1;
            }

            centerRow /= count;
            centerCol /= count;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (regions[row, col] != targetRegion)
                    {
                        continue;
                    }

                    float distance = (row - centerRow) * (row - centerRow) + (col - centerCol) * (col - centerCol);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = row * columns + col;
                    }
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// 创建区域名字标签：黑色加粗文字（无背景条），直接显示在区域中心格子上。
        /// 返回 root（文字子物体铺满，统一管理尺寸/位置/销毁）。
        /// </summary>
        private GameObject CreateRegionLabel(string name)
        {
            TMP_FontAsset font = GetSceneFont();
            if (font == null)
            {
                return null;
            }

            GameObject root = new GameObject("RegionLabel", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(overlayRoot, false);

            GameObject textObject = new GameObject(
                "LabelText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.layer = LayerMask.NameToLayer("UI");
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 56f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            label.text = name;
            label.raycastTarget = false;
            label.fontStyle = FontStyles.Bold;
            return root;
        }

        private static TMP_FontAsset GetSceneFont()
        {
            TextMeshProUGUI anyText = FindFirstObjectByType<TextMeshProUGUI>();
            return anyText == null ? null : anyText.font;
        }
    }
}
