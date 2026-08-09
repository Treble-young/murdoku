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
        private TestBoardController board;

        public event Action<CharacterData> SelectionChanged;

        public CharacterData SelectedCharacter => selectedCard == null ? null : selectedCard.Character;

        public IReadOnlyList<CharacterData> Characters => characters;

        private void Start()
        {
            board = UnityEngine.Object.FindFirstObjectByType<TestBoardController>();
            if (board != null)
            {
                board.GridGenerated += HandleGridGenerated;
            }

            // 根据当前棋盘大小立即重建嫌疑人列表（覆盖默认测试角色）。
            if (board != null)
            {
                RebuildSuspects(board.Rows);
            }
            else
            {
                Rebuild();
            }
        }

        private void OnDestroy()
        {
            if (board != null)
            {
                board.GridGenerated -= HandleGridGenerated;
            }
        }

        private void HandleGridGenerated(int rows, int columns)
        {
            RebuildSuspects(rows);
        }

        /// <summary>
        /// 根据棋盘大小重建嫌疑人列表：N-1 名嫌疑人（A~）+ 1 名受害者（V）。
        /// </summary>
        public void RebuildSuspects(int boardSize)
        {
            SetCharacters(SuspectGenerator.Generate(boardSize));
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
