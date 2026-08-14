using UnityEngine;

namespace Murdoku.Characters
{
    public enum CharacterGender
    {
        Unknown,
        Male,
        Female,
        Other
    }

    [CreateAssetMenu(fileName = "CharacterData", menuName = "Murdoku/Characters/Character Data")]
    public sealed class CharacterData : ScriptableObject
    {
        [SerializeField] private string characterId;
        [SerializeField] private string displayName;
        [SerializeField] private CharacterGender gender;
        [TextArea(2, 5)]
        [SerializeField] private string clue;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Color placeholderColor = new Color(0.35f, 0.55f, 0.85f, 1f);

        public string CharacterId => characterId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public CharacterGender Gender => gender;
        public string Clue => clue;
        public Sprite Portrait => portrait;
        public Color PlaceholderColor => placeholderColor;

        public void SetClue(string text)
        {
            clue = text ?? string.Empty;
        }

        /// <summary>
        /// 运行时设置显示名（载入关卡时恢复出题人设定的名字，避免游玩模式重新随机）。
        /// </summary>
        public void SetDisplayName(string name)
        {
            displayName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }

        /// <summary>
        /// 运行时创建嫌疑人/受害者数据（不保存为 asset，用于出题器按棋盘大小动态生成）。
        /// </summary>
        public static CharacterData CreateRuntime(
            string characterId,
            string displayName,
            CharacterGender gender,
            string clue,
            Color placeholderColor)
        {
            CharacterData data = CreateInstance<CharacterData>();
            data.characterId = characterId;
            data.displayName = displayName;
            data.gender = gender;
            data.clue = clue;
            data.portrait = null;
            data.placeholderColor = placeholderColor;
            return data;
        }

        public string Initial
        {
            get
            {
                string source = DisplayName.Trim();
                return source.Length == 0 ? "?" : source.Substring(0, 1).ToUpperInvariant();
            }
        }

        public string GenderSymbol
        {
            get
            {
                switch (gender)
                {
                    case CharacterGender.Male:
                        return "♂";
                    case CharacterGender.Female:
                        return "♀";
                    case CharacterGender.Other:
                        return "⚧";
                    default:
                        return "?";
                }
            }
        }

        /// <summary>
        /// 在男性/女性之间循环切换（嫌疑人编辑用）。
        /// </summary>
        public void ToggleGender()
        {
            gender = gender == CharacterGender.Female ? CharacterGender.Male : CharacterGender.Female;
        }
    }
}
