using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class TextButtonHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("拖拽按钮内部的TMP文字")]
    public TMP_Text buttonText;

    [Header("颜色设置")]
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(0.3f, 0.6f, 1f);
    public Color pressedColor = Color.gray;

    private Button _btn;

    void Awake()
    {
        _btn = GetComponent<Button>();
        if (buttonText != null)
            buttonText.color = normalColor;
    }

    //鼠标移入
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_btn.interactable && buttonText != null)
            buttonText.color = highlightColor;
    }

    //鼠标移出
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_btn.interactable && buttonText != null)
            buttonText.color = normalColor;
    }

    //鼠标按下
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_btn.interactable && buttonText != null)
            buttonText.color = pressedColor;
    }

    //鼠标抬起
    public void OnPointerUp(PointerEventData eventData)
    {
        if (_btn.interactable && buttonText != null)
            buttonText.color = highlightColor;
    }
}
