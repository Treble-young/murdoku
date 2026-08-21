using System;
using System.Collections.Generic;
using Murdoku.Characters;
using UnityEngine;

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

        /// <summary>角色显示名（出题时设定的名字，游玩模式载入时恢复；旧存档无此字段自动跳过）。</summary>
        public string name;

        /// <summary>角色性别（出题时设定的性别，游玩模式载入时恢复；Unknown = 旧存档未设置，保持随机）。</summary>
        public CharacterGender gender;
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

        /// <summary>
        /// 格子道具，size × size 展平；-1 表示无道具，否则为 PropStyleFactory.All 的索引。
        /// </summary>
        public int[] props;

        /// <summary>
        /// 出题人禁放格（size × size 展平，true = 禁止放置人物，如冰箱/桌子占位）。
        /// 游玩模式隐形生效（不显示黑叉，但格子拒绝放置）。
        /// </summary>
        public bool[] forbiddenCells;

        /// <summary>
        /// 区域名字（按墙计算出的区域 id 索引；空字符串 = 未命名）。旧存档无字段自动兼容。
        /// </summary>
        public List<string> regionNames = new List<string>();

        /// <summary>
        /// 区域名字文字偏移（按区域 id 索引，相对几何中心的像素偏移）。旧存档无字段自动兼容。
        /// </summary>
        public List<Vector2> regionNameOffsets = new List<Vector2>();

        public List<PuzzlePlacementData> placements = new List<PuzzlePlacementData>();
        public List<PuzzleClueData> clues = new List<PuzzleClueData>();

        /// <summary>
        /// 关卡难度：0=教程 1=简单 2=中等 3=困难 4=噩梦。
        /// 旧存档缺省为 0（教程），兼容历史关卡。
        /// </summary>
        public int difficulty;

        /// <summary>
        /// 全局线索（整局提示，如「没有人单独在一个区域」）；空字符串 = 无全局线索，不显示。
        /// 旧存档无此字段自动兼容。
        /// </summary>
        public string globalClue;
    }
}
