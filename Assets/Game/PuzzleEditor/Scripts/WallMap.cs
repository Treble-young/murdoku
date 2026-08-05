using System.Collections.Generic;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 棋盘墙壁数据：N×N 棋盘。
    /// 水平墙：位于 row 与 row+1 之间、col 列（尺寸 (N-1)×N）；
    /// 垂直墙：位于 col 与 col+1 之间、row 行（尺寸 N×(N-1)）。
    /// 墙将棋盘划分为多个连通区域（区域由 ComputeRegions 计算）。
    /// 纯逻辑类，不依赖 Unity 场景对象。
    /// </summary>
    public sealed class WallMap
    {
        private readonly bool[,] horizontalWalls;
        private readonly bool[,] verticalWalls;

        public WallMap(int size)
        {
            if (size < 2)
            {
                throw new System.ArgumentException("棋盘尺寸必须大于等于 2。", nameof(size));
            }

            Size = size;
            horizontalWalls = new bool[size - 1, size];
            verticalWalls = new bool[size, size - 1];
        }

        public int Size { get; }

        public bool GetHorizontalWall(int row, int col)
        {
            return horizontalWalls[row, col];
        }

        public bool GetVerticalWall(int row, int col)
        {
            return verticalWalls[row, col];
        }

        public void SetHorizontalWall(int row, int col, bool isWall)
        {
            horizontalWalls[row, col] = isWall;
        }

        public void SetVerticalWall(int row, int col, bool isWall)
        {
            verticalWalls[row, col] = isWall;
        }

        public void ToggleHorizontalWall(int row, int col)
        {
            horizontalWalls[row, col] = !horizontalWalls[row, col];
        }

        public void ToggleVerticalWall(int row, int col)
        {
            verticalWalls[row, col] = !verticalWalls[row, col];
        }

        /// <summary>
        /// 使用 BFS 计算连通区域，返回 N×N 的数组 [row, col] = 区域编号（从 0 开始）。
        /// </summary>
        public int[,] ComputeRegions()
        {
            int[,] regions = new int[Size, Size];
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    regions[row, col] = -1;
                }
            }

            int nextRegion = 0;
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    if (regions[row, col] != -1)
                    {
                        continue;
                    }

                    FloodFill(row, col, nextRegion, regions);
                    nextRegion++;
                }
            }

            return regions;
        }

        private void FloodFill(int startRow, int startCol, int regionId, int[,] regions)
        {
            Queue<(int Row, int Col)> queue = new Queue<(int Row, int Col)>();
            queue.Enqueue((startRow, startCol));
            regions[startRow, startCol] = regionId;

            while (queue.Count > 0)
            {
                (int row, int col) = queue.Dequeue();

                // 上
                if (row > 0 && !GetHorizontalWall(row - 1, col) && regions[row - 1, col] == -1)
                {
                    regions[row - 1, col] = regionId;
                    queue.Enqueue((row - 1, col));
                }

                // 下
                if (row < Size - 1 && !GetHorizontalWall(row, col) && regions[row + 1, col] == -1)
                {
                    regions[row + 1, col] = regionId;
                    queue.Enqueue((row + 1, col));
                }

                // 左
                if (col > 0 && !GetVerticalWall(row, col - 1) && regions[row, col - 1] == -1)
                {
                    regions[row, col - 1] = regionId;
                    queue.Enqueue((row, col - 1));
                }

                // 右
                if (col < Size - 1 && !GetVerticalWall(row, col) && regions[row, col + 1] == -1)
                {
                    regions[row, col + 1] = regionId;
                    queue.Enqueue((row, col + 1));
                }
            }
        }
    }
}
