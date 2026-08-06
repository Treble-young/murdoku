using System;
using System.Collections.Generic;
using UnityEngine;

namespace Murdoku.Characters
{
    public sealed class CharacterPanelUI : MonoBehaviour
    {
        [SerializeField] private CharacterPanelView view;
        [SerializeField] private CharacterCardUI cardPrefab;
        [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

        private readonly List<CharacterCardUI> cards = new List<CharacterCardUI>();
        private CharacterCardUI selectedCard;

        public event Action<CharacterData> SelectionChanged;

        public CharacterData SelectedCharacter => selectedCard == null ? null : selectedCard.Character;

        public IReadOnlyList<CharacterData> Characters => characters;

        private void Start()
        {
            Rebuild();
        }

        public void SetView(CharacterPanelView panelView)
        {
            view = panelView;
        }

        public void SetCardPrefab(CharacterCardUI prefab)
        {
            cardPrefab = prefab;
        }

        public void SetCharacters(IEnumerable<CharacterData> characterData)
        {
            characters.Clear();
            if (characterData != null)
            {
                characters.AddRange(characterData);
            }
        }

        public void Rebuild()
        {
            ClearCards();

            if (view == null || view.CharacterGrid == null || cardPrefab == null)
            {
                Debug.LogWarning("CharacterPanelUI is missing its view, grid, or card prefab.", this);
                return;
            }

            foreach (CharacterData character in characters)
            {
                if (character == null)
                {
                    continue;
                }

                CharacterCardUI card = Instantiate(cardPrefab, view.CharacterGrid);
                card.name = $"CharacterCard_{character.DisplayName}";
                card.Bind(character, HandleCardClicked, HandleCardDragStarted);
                cards.Add(card);
            }
        }

        private void HandleCardClicked(CharacterCardUI card)
        {
            if (card == null)
            {
                return;
            }

            if (selectedCard == card)
            {
                selectedCard.SetSelected(false);
                selectedCard = null;
                SelectionChanged?.Invoke(null);
                return;
            }

            SelectCard(card);
        }

        private void HandleCardDragStarted(CharacterCardUI card)
        {
            if (card != null && selectedCard != card)
            {
                SelectCard(card);
            }
        }

        private void SelectCard(CharacterCardUI card)
        {
            if (selectedCard != null)
            {
                selectedCard.SetSelected(false);
            }

            selectedCard = card;
            selectedCard.SetSelected(true);
            SelectionChanged?.Invoke(selectedCard.Character);
        }

        private void ClearCards()
        {
            if (selectedCard != null)
            {
                selectedCard = null;
                SelectionChanged?.Invoke(null);
            }

            foreach (CharacterCardUI card in cards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            cards.Clear();
        }
    }
}
