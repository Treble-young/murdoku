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
    }
}
