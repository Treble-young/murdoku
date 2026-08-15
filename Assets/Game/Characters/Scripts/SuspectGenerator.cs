using System.Collections.Generic;
using UnityEngine;

namespace Murdoku.Characters
{
    /// <summary>
    /// 出题器嫌疑人生成器：根据棋盘大小生成嫌疑人（A、B、C…）+ 受害者（V）的运行时角色数据。
    /// 嫌疑人数量 = 棋盘边长 - 1（如 6×6 → 5 名嫌疑人 A~E + 1 名受害者 V）。
    /// </summary>
    public static class SuspectGenerator
    {
        // 每个字母 5 个常用名字（嫌疑人最多到 MaxSize-1=11 名，即字母 A~K）。
        private static readonly Dictionary<char, string[]> NamePool = new Dictionary<char, string[]>
        {
            { 'A', new[] { "Alan", "Andy", "Alice", "Amy", "Alex" } },
            { 'B', new[] { "Bob", "Ben", "Betty", "Brian", "Bella" } },
            { 'C', new[] { "Carl", "Cathy", "Cindy", "Chris", "Carol" } },
            { 'D', new[] { "Dave", "Diana", "Danny", "Doris", "Duke" } },
            { 'E', new[] { "Emma", "Eric", "Eva", "Eddie", "Ella" } },
            { 'F', new[] { "Frank", "Fiona", "Fred", "Faith", "Felix" } },
            { 'G', new[] { "Gary", "Grace", "Gina", "George", "Gwen" } },
            { 'H', new[] { "Henry", "Helen", "Hugo", "Hannah", "Hank" } },
            { 'I', new[] { "Ivan", "Ivy", "Ian", "Iris", "Ingrid" } },
            { 'J', new[] { "Jack", "Jane", "John", "Julia", "Jerry" } },
            { 'K', new[] { "Kevin", "Kate", "Ken", "Karen", "Kyle" } }
        };

        // 嫌疑人卡片色池（饱和度适中的浅色，与区域着色的淡色区分开）。
        private static readonly Color[] CardColors =
        {
            new Color(0.42f, 0.62f, 0.88f, 1f), // 蓝
            new Color(0.48f, 0.78f, 0.55f, 1f), // 绿
            new Color(0.92f, 0.55f, 0.62f, 1f), // 粉
            new Color(0.68f, 0.55f, 0.88f, 1f), // 紫
            new Color(0.95f, 0.68f, 0.42f, 1f), // 橙
            new Color(0.45f, 0.82f, 0.80f, 1f), // 青
            new Color(0.88f, 0.82f, 0.48f, 1f), // 黄
            new Color(0.60f, 0.70f, 0.92f, 1f), // 靛蓝
            new Color(0.85f, 0.60f, 0.72f, 1f), // 玫红
            new Color(0.58f, 0.85f, 0.65f, 1f), // 浅绿
            new Color(0.80f, 0.65f, 0.50f, 1f)  // 棕
        };

        private static readonly Color VictimColor = new Color(0.52f, 0.42f, 0.45f, 1f);

        /// <summary>
        /// 按棋盘边长生成嫌疑人列表（N-1 名嫌疑人 A~ + 1 名受害者 V）。
        /// 生成的是运行时数据（不保存为 asset）。
        /// </summary>
        public static List<CharacterData> Generate(int boardSize)
        {
            int suspectCount = Mathf.Max(1, boardSize - 1);
            var result = new List<CharacterData>(suspectCount + 1);

            for (int i = 0; i < suspectCount; i++)
            {
                char letter = (char)('A' + i);
                string displayName = PickName(letter);
                CharacterGender gender = RandomGender();
                Color color = CardColors[i % CardColors.Length];

                CharacterData suspect = CharacterData.CreateRuntime(
                    letter.ToString(), displayName, gender, string.Empty, color);
                suspect.name = $"Suspect_{letter}";
                result.Add(suspect);
            }

            CharacterData victim = CharacterData.CreateRuntime(
                "V", "Victim", RandomGender(), string.Empty, VictimColor);
            victim.name = "Victim_V";
            result.Add(victim);

            return result;
        }

        // 名字 → 性别映射（名字池中每个名字的性别；旧存档缺性别时按名字推断）。
        private static readonly Dictionary<string, CharacterGender> GenderMap = new Dictionary<string, CharacterGender>
        {
            { "Alan", CharacterGender.Male }, { "Andy", CharacterGender.Male }, { "Alice", CharacterGender.Female }, { "Amy", CharacterGender.Female }, { "Alex", CharacterGender.Male },
            { "Bob", CharacterGender.Male }, { "Ben", CharacterGender.Male }, { "Betty", CharacterGender.Female }, { "Brian", CharacterGender.Male }, { "Bella", CharacterGender.Female },
            { "Carl", CharacterGender.Male }, { "Cathy", CharacterGender.Female }, { "Cindy", CharacterGender.Female }, { "Chris", CharacterGender.Male }, { "Carol", CharacterGender.Female },
            { "Dave", CharacterGender.Male }, { "Diana", CharacterGender.Female }, { "Danny", CharacterGender.Male }, { "Doris", CharacterGender.Female }, { "Duke", CharacterGender.Male },
            { "Emma", CharacterGender.Female }, { "Eric", CharacterGender.Male }, { "Eva", CharacterGender.Female }, { "Eddie", CharacterGender.Male }, { "Ella", CharacterGender.Female },
            { "Frank", CharacterGender.Male }, { "Fiona", CharacterGender.Female }, { "Fred", CharacterGender.Male }, { "Faith", CharacterGender.Female }, { "Felix", CharacterGender.Male },
            { "Gary", CharacterGender.Male }, { "Grace", CharacterGender.Female }, { "Gina", CharacterGender.Female }, { "George", CharacterGender.Male }, { "Gwen", CharacterGender.Female },
            { "Henry", CharacterGender.Male }, { "Helen", CharacterGender.Female }, { "Hugo", CharacterGender.Male }, { "Hannah", CharacterGender.Female }, { "Hank", CharacterGender.Male },
            { "Ivan", CharacterGender.Male }, { "Ivy", CharacterGender.Female }, { "Ian", CharacterGender.Male }, { "Iris", CharacterGender.Female }, { "Ingrid", CharacterGender.Female },
            { "Jack", CharacterGender.Male }, { "Jane", CharacterGender.Female }, { "John", CharacterGender.Male }, { "Julia", CharacterGender.Female }, { "Jerry", CharacterGender.Male },
            { "Kevin", CharacterGender.Male }, { "Kate", CharacterGender.Female }, { "Ken", CharacterGender.Male }, { "Karen", CharacterGender.Female }, { "Kyle", CharacterGender.Male }
        };

        /// <summary>
        /// 按名字推断性别（名字池中的名字自带性别；未知名字返回 Unknown）。
        /// 用于为旧存档（无性别字段）补充性别数据。
        /// </summary>
        public static CharacterGender InferGenderFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return CharacterGender.Unknown;
            }

            return GenderMap.TryGetValue(name.Trim(), out CharacterGender gender) ? gender : CharacterGender.Unknown;
        }

        private static string PickName(char letter)
        {
            if (!NamePool.TryGetValue(letter, out string[] names) || names.Length == 0)
            {
                return letter.ToString();
            }

            return names[Random.Range(0, names.Length)];
        }

        private static CharacterGender RandomGender()
        {
            return Random.value < 0.5f ? CharacterGender.Male : CharacterGender.Female;
        }
    }
}
