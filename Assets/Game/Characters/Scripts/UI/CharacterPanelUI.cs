using System;
using System.Collections.Generic;
using Murdoku.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class CharacterPanelUI : MonoBehaviour
    {
        [SerializeField] private CharacterPanelView view;
        [SerializeField] private CharacterCardUI cardPrefab;
        [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

        private readonly List<CharacterCardUI> cards = new List<CharacterCardUI>();
        private readonly HashSet<CharacterData> placedCharacters = new HashSet<CharacterData>();
        private CharacterCardUI selectedCard;
        private TestBoardController board;
        private GameObject blackXCard;
        private GameObject blackXBorder;
        private bool blackXActive;
        private Coroutine blackXScaleRoutine;
        private TMP_Text globalClueText;
        private const float BlackXSelectedScale = 1.1f;
        private const float BlackXScaleDuration = 0.1f;

        public event Action<CharacterData> SelectionChanged;

        /// <summary>黑叉模式切换事件（true = 禁止放置模式激活）。</summary>
        public event Action<bool> BlackXModeChanged;

        public CharacterData SelectedCharacter => selectedCard == null ? null : selectedCard.Character;

        /// <summary>是否处于「禁止放置」打叉模式。</summary>
        public bool BlackXActive => blackXActive;

        public IReadOnlyList<CharacterData> Characters => characters;

        /// <summary>
        /// 把存档中的角色线索写回运行时角色数据，并刷新所有卡片的线索文本。
        /// </summary>
        public void ApplyClues(IEnumerable<PuzzleClueData> clues)
        {
            if (clues == null)
            {
                return;
            }

            foreach (PuzzleClueData clue in clues)
            {
                if (clue == null || string.IsNullOrEmpty(clue.characterId))
                {
                    continue;
                }

                foreach (CharacterData character in characters)
                {
                    if (character == null)
                    {
                        continue;
                    }

                    if (string.Equals(character.CharacterId, clue.characterId, StringComparison.OrdinalIgnoreCase))
                    {
                        // 恢复出题人设定的名字/性别（旧存档无字段则按名字推断性别，保持名字）。
                        if (!string.IsNullOrWhiteSpace(clue.name))
                        {
                            character.SetDisplayName(clue.name);
                        }

                        if (clue.gender != CharacterGender.Unknown)
                        {
                            character.SetGender(clue.gender);
                        }
                        else
                        {
                            // 旧存档没有性别字段：按名字推断补充（重新保存后即固定入档）。
                            CharacterGender inferred = SuspectGenerator.InferGenderFromName(character.DisplayName);
                            if (inferred != CharacterGender.Unknown)
                            {
                                character.SetGender(inferred);
                            }
                        }

                        character.SetClue(clue.clue);
                        break;
                    }
                }
            }

            RefreshAllClues();
            RefreshAllNames();
            RefreshAllGenders();
        }

        public void RefreshAllGenders()
        {
            foreach (CharacterCardUI card in cards)
            {
                if (card != null)
                {
                    card.RefreshGender();
                }
            }
        }

        /// <summary>
        /// 设置全局线索文本（显示在嫌疑人卡片下方）；空字符串隐藏。
        /// </summary>
        public void SetGlobalClue(string clue)
        {
            string text = string.IsNullOrWhiteSpace(clue) ? string.Empty : clue.Trim();
            if (text.Length == 0)
            {
                if (globalClueText != null)
                {
                    globalClueText.gameObject.SetActive(false);
                }

                return;
            }

            EnsureGlobalClueText();
            if (globalClueText != null)
            {
                globalClueText.text = text;
                globalClueText.gameObject.SetActive(true);
            }
        }

        private void EnsureGlobalClueText()
        {
            if (globalClueText != null)
            {
                return;
            }

            Transform parent = view == null ? null : view.CharacterGrid;
            if (parent == null)
            {
                return;
            }

            RectTransform gridRect = view.CharacterGrid;
            TMP_FontAsset font = FindFirstObjectByType<TextMeshProUGUI>().font;

            GameObject backgroundObject = new GameObject("GlobalClue", typeof(RectTransform), typeof(Image));
            backgroundObject.layer = LayerMask.NameToLayer("UI");
            RectTransform backgroundRect = (RectTransform)backgroundObject.transform;
            backgroundRect.SetParent(gridRect.parent, false);
            backgroundRect.anchorMin = new Vector2(0.5f, 0f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0f);
            backgroundRect.pivot = new Vector2(0.5f, 0f);
            backgroundRect.sizeDelta = new Vector2(820f, 66f);
            backgroundRect.anchoredPosition = new Vector2(0f, 14f);

            Image background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.13f, 0.15f, 0.20f, 0.92f);
            background.raycastTarget = false;

            GameObject labelObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = LayerMask.NameToLayer("UI");
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(backgroundRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);

            TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 22f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            globalClueText = label;
        }

        public void RefreshAllNames()
        {
            foreach (CharacterCardUI card in cards)
            {
                if (card != null)
                {
                    card.RefreshName();
                }
            }
        }

        public void RefreshAllClues()
        {
            foreach (CharacterCardUI card in cards)
            {
                if (card != null)
                {
                    card.RefreshClue();
                }
            }
        }

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
            placedCharacters.Clear();
            if (characterData != null)
            {
                characters.AddRange(characterData);
            }
        }

        /// <summary>
        /// 同步人物是否已经放入棋盘，并立即刷新对应的左侧卡片。
        /// </summary>
        public void SetCharacterPlaced(CharacterData character, bool placed)
        {
            if (character == null)
            {
                return;
            }

            if (placed)
            {
                placedCharacters.Add(character);
            }
            else
            {
                placedCharacters.Remove(character);
            }

            foreach (CharacterCardUI card in cards)
            {
                if (card != null && ReferenceEquals(card.Character, character))
                {
                    card.SetPlaced(placed);
                    break;
                }
            }
        }

        public bool IsCharacterPlaced(CharacterData character)
        {
            return character != null && placedCharacters.Contains(character);
        }

        /// <summary>
        /// 取消当前选中的人物卡（用于与地块选择互斥）。
        /// </summary>
        public void ClearSelection()
        {
            if (selectedCard == null)
            {
                return;
            }

            selectedCard.SetSelected(false);
            selectedCard = null;
            SelectionChanged?.Invoke(null);
        }

        private bool genderToggleEnabled = true;

        /// <summary>
        /// 批量控制所有嫌疑人卡的性别切换按钮（出题模式启用，游玩模式禁用）。
        /// 状态持久化：之后 Rebuild 生成的卡片也会应用该状态。
        /// </summary>
        public void SetGenderToggleEnabled(bool enabled)
        {
            genderToggleEnabled = enabled;
            foreach (CharacterCardUI card in cards)
            {
                if (card != null)
                {
                    card.SetGenderToggleEnabled(enabled);
                }
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
                card.SetGenderToggleEnabled(genderToggleEnabled);
                card.SetPlaced(placedCharacters.Contains(character));
                cards.Add(card);
            }

            CreateBlackXCard();
        }

        /// <summary>
        /// 在网格末尾创建「禁止放置」黑叉卡，结构与嫌疑人卡一致：
        /// 图案（黑色 ×，上部）+ 名字（禁用标记）+ 线索文本（默认提示，底部）+ 蓝色细边框（选中时显示）。
        /// 出题模式：标记禁放格（保存为规则）；游玩模式：玩家标记已排除区域（不保存）。
        /// </summary>
        private void CreateBlackXCard()
        {
            if (view == null || view.CharacterGrid == null)
            {
                return;
            }

            TMP_FontAsset font = FindFirstObjectByType<TextMeshProUGUI>().font;
            Color darkText = new Color(0.16f, 0.20f, 0.26f, 1f);

            blackXCard = new GameObject("BlackXCard", typeof(RectTransform), typeof(Image));
            blackXCard.layer = LayerMask.NameToLayer("UI");
            RectTransform root = blackXCard.GetComponent<RectTransform>();
            root.SetParent(view.CharacterGrid, false);

            Image background = blackXCard.GetComponent<Image>();
            background.color = Color.white;
            background.raycastTarget = true;

            // 选中边框：4 条蓝色细边（与嫌疑人卡一致），激活时显示。
            blackXBorder = new GameObject("SelectionBorder", typeof(RectTransform));
            blackXBorder.layer = LayerMask.NameToLayer("UI");
            RectTransform borderRect = blackXBorder.GetComponent<RectTransform>();
            borderRect.SetParent(root, false);
            Stretch(borderRect);

            Color borderColor = new Color(0.22f, 0.48f, 0.86f, 1f);
            CreateBorderBar(borderRect, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 6f), new Vector2(0f, -3f), borderColor);
            CreateBorderBar(borderRect, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 3f), borderColor);
            CreateBorderBar(borderRect, "LeftBar", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(3f, 0f), borderColor);
            CreateBorderBar(borderRect, "RightBar", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-3f, 0f), borderColor);
            blackXBorder.SetActive(false);

            // 图案底图：浅灰色方形（与嫌疑人卡的头像区一致，保证卡面统一）。
            RectTransform markBg = CreateCardRect("MarkBg", root, 124f, 128f);
            markBg.anchorMin = new Vector2(0.5f, 1f);
            markBg.anchorMax = new Vector2(0.5f, 1f);
            markBg.pivot = new Vector2(0.5f, 1f);
            markBg.anchoredPosition = new Vector2(0f, -22f);
            Image markBgImage = markBg.gameObject.AddComponent<Image>();
            markBgImage.color = new Color(0.90f, 0.91f, 0.93f, 1f);
            markBgImage.raycastTarget = false;

            // 图案：黑色大 ×（浅灰底图内）。
            TMP_Text mark = CreateCardText("Mark", markBg, "×", font, 95f, Color.black);
            Stretch(mark.rectTransform);
            mark.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            mark.fontStyle = FontStyles.Bold;

            // 名字：禁用标记（与嫌疑人卡名字同位置/字号：锚顶部 y-148、25 号）。
            TMP_Text nameText = CreateCardText("NameText", root, "禁用标记", font, 25f, darkText);
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(145f, 38f);
            nameRect.anchoredPosition = new Vector2(0f, -148f);
            nameText.fontStyle = FontStyles.Bold;

            // 线索文本：默认提示（名字下方居中区域，自动换行，不贴卡片底部）。
            TMP_Text clueText = CreateCardText("ClueText", root, "这是用于排除或禁止放置的标记", font, 20f, new Color(0.40f, 0.44f, 0.52f, 1f));
            RectTransform clueRect = clueText.rectTransform;
            clueRect.anchorMin = new Vector2(0.5f, 0f);
            clueRect.anchorMax = new Vector2(0.5f, 0f);
            clueRect.pivot = new Vector2(0.5f, 0.5f);
            clueRect.sizeDelta = new Vector2(148f, 56f);
            clueRect.anchoredPosition = new Vector2(0f, 62f);
            clueText.alignment = TextAlignmentOptions.Center;
            clueText.textWrappingMode = TextWrappingModes.Normal;

            BlackXCardHandler handler = blackXCard.AddComponent<BlackXCardHandler>();
            handler.Clicked = ToggleBlackX;
        }

        /// <summary>创建选中边框的一条边（细条 Image）。</summary>
        private static void CreateBorderBar(
            RectTransform parent,
            string barName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition,
            Color color)
        {
            GameObject bar = new GameObject(barName, typeof(RectTransform), typeof(Image));
            bar.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)bar.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;

            Image image = bar.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>创建指定尺寸的矩形（锚点/位置由调用方设置）。</summary>
        private static RectTransform CreateCardRect(string name, RectTransform parent, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static TMP_Text CreateCardText(
            string name,
            RectTransform parent,
            string content,
            TMP_FontAsset font,
            float fontSize,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            TMP_Text text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 切换黑叉模式（点黑叉卡：激活/取消）。
        /// </summary>
        public void ToggleBlackX()
        {
            GameAudio.Play(SfxCue.UiClick);
            SetBlackXActive(!blackXActive);
        }

        /// <summary>
        /// 设置黑叉模式：激活时取消角色选择（互斥），通知协调器。
        /// </summary>
        public void SetBlackXActive(bool active)
        {
            if (blackXActive == active)
            {
                return;
            }

            blackXActive = active;
            if (blackXActive && selectedCard != null)
            {
                selectedCard.SetSelected(false);
                selectedCard = null;
            }

            RefreshBlackXCard();
            SelectionChanged?.Invoke(null);
            BlackXModeChanged?.Invoke(blackXActive);
        }

        private void RefreshBlackXCard()
        {
            if (blackXBorder != null)
            {
                blackXBorder.SetActive(blackXActive);
            }

            // 选中时略微放大（与嫌疑人卡一致）。
            AnimateBlackXScale(blackXActive ? BlackXSelectedScale : 1f);
        }

        private void AnimateBlackXScale(float targetScale)
        {
            if (blackXCard == null)
            {
                return;
            }

            if (blackXScaleRoutine != null)
            {
                StopCoroutine(blackXScaleRoutine);
                blackXScaleRoutine = null;
            }

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                blackXCard.transform.localScale = Vector3.one * targetScale;
                return;
            }

            blackXScaleRoutine = StartCoroutine(AnimateBlackXScaleRoutine(targetScale));
        }

        private System.Collections.IEnumerator AnimateBlackXScaleRoutine(float targetScale)
        {
            Vector3 startScale = blackXCard.transform.localScale;
            Vector3 endScale = Vector3.one * targetScale;
            float elapsed = 0f;

            while (elapsed < BlackXScaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / BlackXScaleDuration);
                blackXCard.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            blackXCard.transform.localScale = endScale;
            blackXScaleRoutine = null;
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
            // 选中角色时退出黑叉模式（互斥）。
            if (blackXActive)
            {
                SetBlackXActive(false);
            }

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
            if (blackXActive)
            {
                blackXActive = false;
                BlackXModeChanged?.Invoke(false);
            }

            if (blackXCard != null)
            {
                Destroy(blackXCard);
                blackXCard = null;
                blackXBorder = null;
            }

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
