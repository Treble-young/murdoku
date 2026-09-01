using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    private const string FeedbackContent =
        "首先，衷心感谢每一位玩家的支持与厚爱！你们的每一次点击、每一份关注，都是对我们莫大的鼓励。\n\n" +
        "我们游戏的玩法灵感，来源于海外网站 Murdoku（https://murdoku.com/play/）（该网站目前在国内可能无法正常访问）。这是一款非常优秀的作品，我们选择它作为第一个开发项目，纯粹是为了学习和实践游戏开发，同时也是希望将这样有趣的玩法介绍给更多朋友。<b>因此，我们的游戏将完全免费，不会收取费用</b>。如果您对这类玩法本身感兴趣，我们非常欢迎您通过合法途径去了解和支持原作者，他们的创意才是真正的源头。\n\n" +
        "作为第一次制作游戏的学生团队，我们在开发过程中难免存在疏漏和不足，由此给各位带来的不便和困扰，我们深表歉意。我们珍视每一次试错与成长的机会。\n\n" +
        "如果你在游玩中有什么想法，无论是改进建议、问题反馈，还是希望分享自己创作的关卡，都非常欢迎通过邮箱联系我们：<b>1436783805@qq.com</b>。每一条声音我们都会认真阅读、悉心记录。\n\n" +
        "此外，我们也在寻找志同道合的伙伴——<b>如果你是中山大学的学生，热爱游戏创作，无论专业背景，无论是否有经验</b>，我们都热切期待你的加入！策划、文案、美术……每一个岗位都有你的舞台。即便你是零基础新手，我们也会手把手带你学习游戏开发基础、Git 项目管理与团队协作方法。一起从零起步，共同打造属于我们的游戏世界。\n\n" +
        "期待你的来信，更期待与你并肩同行！";

    [Header("拖拽绑定")]
    public GameObject instructionPanel;
    public GameObject maskBlack;    // 把遮罩面板拖进这个槽
    [SerializeField] private TMP_FontAsset feedbackFont;

    private GameObject feedbackPanel;

    void Start()
    {
        instructionPanel.SetActive(false);
        maskBlack.SetActive(false);
        CreateFeedbackButton();
        CreateFeedbackPanel();
    }

    // 打开教程：同时显示遮罩+弹窗
    public void OpenInstruction()
    {
        maskBlack.SetActive(true);
        instructionPanel.SetActive(true);
    }

    // 关闭教程：同时隐藏遮罩+弹窗
    public void CloseInstruction()
    {
        maskBlack.SetActive(false);
        instructionPanel.SetActive(false);
    }

    public void OpenFeedback()
    {
        if (feedbackPanel == null)
        {
            return;
        }

        maskBlack.SetActive(true);
        feedbackPanel.SetActive(true);
        feedbackPanel.transform.SetAsLastSibling();
    }

    public void CloseFeedback()
    {
        if (feedbackPanel == null)
        {
            return;
        }

        feedbackPanel.SetActive(false);
        maskBlack.SetActive(false);
    }

    private void CreateFeedbackButton()
    {
        GameObject createButtonObject = GameObject.Find("create");
        GameObject quitButtonObject = GameObject.Find("end");
        if (createButtonObject == null || quitButtonObject == null)
        {
            Debug.LogWarning("未找到主菜单的谜题创建或退出游戏按钮，无法创建内容反馈按钮。");
            return;
        }

        RectTransform createRect = createButtonObject.GetComponent<RectTransform>();
        RectTransform quitRect = quitButtonObject.GetComponent<RectTransform>();
        quitRect.anchoredPosition = createRect.anchoredPosition + Vector2.down * 280f;

        GameObject feedbackButtonObject = Instantiate(createButtonObject, createButtonObject.transform.parent);
        feedbackButtonObject.name = "feedback";
        RectTransform feedbackRect = feedbackButtonObject.GetComponent<RectTransform>();
        feedbackRect.anchoredPosition = createRect.anchoredPosition + Vector2.down * 140f;

        TMP_Text label = feedbackButtonObject.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = "内容反馈";
        }

        Button button = feedbackButtonObject.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OpenFeedback);
    }

    private void CreateFeedbackPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("未找到主菜单 Canvas，无法创建内容反馈窗口。");
            return;
        }

        feedbackPanel = CreateUIObject("FeedbackPanel", canvas.transform);
        RectTransform panelRect = feedbackPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1420f, 860f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = feedbackPanel.AddComponent<Image>();
        panelImage.color = new Color(0.85f, 0.87f, 0.90f, 1f);
        Murdoku.UiRoundedSprite.Apply(panelImage, 20);

        TMP_Text referenceText = instructionPanel.GetComponentInChildren<TMP_Text>(true);
        TMP_Text title = CreateText("Title", feedbackPanel.transform, referenceText);
        title.text = "内容反馈";
        title.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        title.fontSize = 44f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -32f);
        title.rectTransform.sizeDelta = new Vector2(-120f, 80f);

        GameObject scrollObject = CreateUIObject("Scroll View", feedbackPanel.transform);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetRect(scrollRectTransform, new Vector2(80f, 115f), new Vector2(-80f, -135f));
        Image viewportImage = scrollObject.AddComponent<Image>();
        viewportImage.color = new Color(0.93f, 0.94f, 0.96f, 1f);
        Murdoku.UiRoundedSprite.Apply(viewportImage, 16);
        Mask mask = scrollObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject contentObject = CreateUIObject("Content", scrollObject.transform);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = new Vector2(0f, -22f);
        contentRect.sizeDelta = new Vector2(-70f, 1040f);

        TMP_Text content = CreateText("FeedbackText", contentObject.transform, referenceText);
        content.text = FeedbackContent;
        content.color = new Color(0.14f, 0.14f, 0.14f, 1f);
        content.fontSize = 40f;
        content.fontStyle = FontStyles.Normal;
        content.alignment = TextAlignmentOptions.TopLeft;
        content.lineSpacing = 10f;
        content.enableWordWrapping = true;
        content.rectTransform.anchorMin = Vector2.zero;
        content.rectTransform.anchorMax = Vector2.one;
        content.rectTransform.offsetMin = Vector2.zero;
        content.rectTransform.offsetMax = Vector2.zero;

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.viewport = scrollRectTransform;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 35f;

        GameObject closeObject = CreateUIObject("CloseButton", feedbackPanel.transform);
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(240f, 76f);
        closeRect.anchoredPosition = new Vector2(0f, 55f);
        Image closeImage = closeObject.AddComponent<Image>();
        closeImage.color = new Color(0.20f, 0.20f, 0.20f, 0.92f);
        Murdoku.UiRoundedSprite.Apply(closeImage, 14);
        Button closeButton = closeObject.AddComponent<Button>();
        closeButton.onClick.AddListener(CloseFeedback);
        TMP_Text closeLabel = CreateText("Label", closeObject.transform, referenceText);
        closeLabel.text = "关闭";
        closeLabel.fontSize = 30f;
        closeLabel.color = Color.white;
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.rectTransform.anchorMin = Vector2.zero;
        closeLabel.rectTransform.anchorMax = Vector2.one;
        closeLabel.rectTransform.offsetMin = Vector2.zero;
        closeLabel.rectTransform.offsetMax = Vector2.zero;

        feedbackPanel.SetActive(false);
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.layer = LayerMask.NameToLayer("UI");
        result.transform.SetParent(parent, false);
        return result;
    }

    private TMP_Text CreateText(string objectName, Transform parent, TMP_Text reference)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (feedbackFont != null)
        {
            text.font = feedbackFont;
        }
        else if (reference != null)
        {
            text.font = reference.font;
        }
        text.color = new Color(0.16f, 0.16f, 0.16f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public void OnStartGame()
    {
        Debug.Log("点击开始游戏，准备进入对局");
        SceneManager.LoadScene("LevelSelectScene");
    }

    public void OnCreatePuzzle()
    {
        Murdoku.PuzzleSession.SelectedPuzzleId = null;
        SceneManager.LoadScene("PuzzleScene");
    }

    public void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
