using System;
using System.Collections.Generic;

namespace Murdoku
{
    /// <summary>
    /// 玩家出题数据：棋盘大小 + 墙体（区域划分）+ 角色题设。
    /// 纯数据类，用于 JsonUtility 序列化，不引用场景对象。
    /// </summary>
    [Serializable]
    public sealed class PuzzlePlacementData
    {
        public string characterId;
        public int cellIndex;
    }

    [Serializable]
    public sealed class PuzzleClueData
    {
        public string characterId;
        public string clue;
    }

    [Serializable]
    public sealed class PuzzleData
    {
        public string id;
        public string name;
        public int size;

        /// <summary>水平墙，(size-1) × size，行优先展平。</summary>
        public bool[] horizontalWalls;

        /// <summary>垂直墙，size × (size-1)，行优先展平。</summary>
        public bool[] verticalWalls;

        /// <summary>
        /// 格子地块，size × size 展平；-1 表示无地块，否则为 RegionStyleFactory.All 的索引。
        /// </summary>
        public int[] floorTiles;

        public List<PuzzlePlacementData> placements = new List<PuzzlePlacementData>();
        public List<PuzzleClueData> clues = new List<PuzzleClueData>();
    }
}
