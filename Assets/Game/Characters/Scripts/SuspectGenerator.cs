using System.Collections.Generic;
using UnityEngine;

namespace Murdoku.Characters
{
    /// <summary>
    /// 根据棋盘大小生成嫌疑人（A、B、C…）和受害者（V）的运行时数据。
    /// 有头像目录时，头像按性别无放回随机抽取；没有目录时保留首字母占位回退。
    /// </summary>
    public static class SuspectGenerator
    {
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

        private static readonly Color[] CardColors =
        {
            new Color(0.42f, 0.62f, 0.88f, 1f),
            new Color(0.48f, 0.78f, 0.55f, 1f),
            new Color(0.92f, 0.55f, 0.62f, 1f),
            new Color(0.68f, 0.55f, 0.88f, 1f),
            new Color(0.95f, 0.68f, 0.42f, 1f),
            new Color(0.45f, 0.82f, 0.80f, 1f),
            new Color(0.88f, 0.82f, 0.48f, 1f),
            new Color(0.60f, 0.70f, 0.92f, 1f),
            new Color(0.85f, 0.60f, 0.72f, 1f),
            new Color(0.58f, 0.85f, 0.65f, 1f),
            new Color(0.80f, 0.65f, 0.50f, 1f)
        };

        private static readonly Color VictimColor = new Color(0.52f, 0.42f, 0.45f, 1f);

        private static readonly Dictionary<string, CharacterGender> GenderMap =
            new Dictionary<string, CharacterGender>
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

        public static List<CharacterData> Generate(int boardSize)
        {
            return Generate(boardSize, null);
        }

        public static List<CharacterData> Generate(
            int boardSize,
            CharacterPortraitCatalog portraitCatalog)
        {
            int suspectCount = Mathf.Max(1, boardSize - 1);
            var result = new List<CharacterData>(suspectCount + 1);
            var availablePortraits = new List<CharacterPortraitCatalog.Entry>();
            portraitCatalog?.CopyUsableEntriesTo(availablePortraits);

            for (int index = 0; index < suspectCount; index++)
            {
                char letter = (char)('A' + index);
                CharacterPortraitCatalog.Entry portrait = TakeRandomPortrait(availablePortraits);
                CharacterGender gender = portrait == null ? RandomGender() : portrait.Gender;
                string displayName = PickName(letter, gender);

                CharacterData suspect = CharacterData.CreateRuntime(
                    letter.ToString(),
                    displayName,
                    gender,
                    string.Empty,
                    CardColors[index % CardColors.Length],
                    portrait == null ? null : portrait.Portrait);
                suspect.name = $"Suspect_{letter}";
                result.Add(suspect);
            }

            CharacterPortraitCatalog.Entry victimPortrait = TakeRandomPortrait(availablePortraits);
            CharacterGender victimGender = victimPortrait == null ? RandomGender() : victimPortrait.Gender;
            CharacterData victim = CharacterData.CreateRuntime(
                "V",
                "Victim",
                victimGender,
                string.Empty,
                VictimColor,
                victimPortrait == null ? null : victimPortrait.Portrait);
            victim.name = "Victim_V";
            result.Add(victim);

            return result;
        }

        /// <summary>
        /// 保留仍然有效且不重复的头像，只为缺失或性别不匹配的人物重新抽取。
        /// 手动切换性别导致同性别头像数量不足时，允许复用同性别头像，绝不回退到首字母占位。
        /// </summary>
        public static void RepairPortraitAssignments(
            IReadOnlyList<CharacterData> characters,
            CharacterPortraitCatalog portraitCatalog)
        {
            if (characters == null || portraitCatalog == null)
            {
                return;
            }

            var availablePortraits = new List<CharacterPortraitCatalog.Entry>();
            portraitCatalog?.CopyUsableEntriesTo(availablePortraits);

            foreach (CharacterData character in characters)
            {
                if (character == null ||
                    character.Portrait == null ||
                    !portraitCatalog.TryGetEntry(character.Portrait, out CharacterPortraitCatalog.Entry entry) ||
                    entry.Gender != character.Gender ||
                    !availablePortraits.Remove(entry))
                {
                    character?.SetPortrait(null);
                }
            }

            foreach (CharacterData character in characters)
            {
                if (character == null || character.Portrait != null)
                {
                    continue;
                }

                CharacterPortraitCatalog.Entry entry = TakeRandomPortrait(
                    availablePortraits,
                    character.Gender);
                if (entry == null)
                {
                    entry = PickRandomPortrait(portraitCatalog.Entries, character.Gender);
                }

                character.SetPortrait(entry == null ? null : entry.Portrait);
            }
        }

        public static CharacterGender InferGenderFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return CharacterGender.Unknown;
            }

            return GenderMap.TryGetValue(name.Trim(), out CharacterGender gender)
                ? gender
                : CharacterGender.Unknown;
        }

        private static string PickName(char letter, CharacterGender gender)
        {
            if (!NamePool.TryGetValue(letter, out string[] names) || names.Length == 0)
            {
                return letter.ToString();
            }

            var matchingNames = new List<string>();
            foreach (string candidate in names)
            {
                if (GenderMap.TryGetValue(candidate, out CharacterGender candidateGender) &&
                    candidateGender == gender)
                {
                    matchingNames.Add(candidate);
                }
            }

            return matchingNames.Count == 0
                ? names[Random.Range(0, names.Length)]
                : matchingNames[Random.Range(0, matchingNames.Count)];
        }

        private static CharacterPortraitCatalog.Entry TakeRandomPortrait(
            List<CharacterPortraitCatalog.Entry> availablePortraits)
        {
            bool hasMale = HasGender(availablePortraits, CharacterGender.Male);
            bool hasFemale = HasGender(availablePortraits, CharacterGender.Female);
            if (!hasMale && !hasFemale)
            {
                return null;
            }

            CharacterGender gender = hasMale && hasFemale
                ? (Random.value < 0.5f ? CharacterGender.Male : CharacterGender.Female)
                : (hasMale ? CharacterGender.Male : CharacterGender.Female);
            return TakeRandomPortrait(availablePortraits, gender);
        }

        private static CharacterPortraitCatalog.Entry TakeRandomPortrait(
            List<CharacterPortraitCatalog.Entry> availablePortraits,
            CharacterGender gender)
        {
            var candidateIndexes = new List<int>();
            for (int index = 0; index < availablePortraits.Count; index++)
            {
                if (availablePortraits[index].Gender == gender)
                {
                    candidateIndexes.Add(index);
                }
            }

            if (candidateIndexes.Count == 0)
            {
                return null;
            }

            int selectedIndex = candidateIndexes[Random.Range(0, candidateIndexes.Count)];
            CharacterPortraitCatalog.Entry selected = availablePortraits[selectedIndex];
            availablePortraits.RemoveAt(selectedIndex);
            return selected;
        }

        private static CharacterPortraitCatalog.Entry PickRandomPortrait(
            IReadOnlyList<CharacterPortraitCatalog.Entry> portraitEntries,
            CharacterGender gender)
        {
            var candidates = new List<CharacterPortraitCatalog.Entry>();
            foreach (CharacterPortraitCatalog.Entry entry in portraitEntries)
            {
                if (entry != null && entry.IsUsable && entry.Gender == gender)
                {
                    candidates.Add(entry);
                }
            }

            return candidates.Count == 0
                ? null
                : candidates[Random.Range(0, candidates.Count)];
        }

        private static bool HasGender(
            List<CharacterPortraitCatalog.Entry> availablePortraits,
            CharacterGender gender)
        {
            foreach (CharacterPortraitCatalog.Entry entry in availablePortraits)
            {
                if (entry.Gender == gender)
                {
                    return true;
                }
            }

            return false;
        }

        private static CharacterGender RandomGender()
        {
            return Random.value < 0.5f ? CharacterGender.Male : CharacterGender.Female;
        }
    }
}
