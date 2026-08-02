using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    [SerializeField] private Color _baseColor, _offsetColor, _highlightColor;
    [SerializeField] private SpriteRenderer _renderer;

    private Color _originalColor; // 记录格子原本的颜色

    void Awake()
    {
        // 如果没拖拽赋值，自动获取
        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();
    }


    public void Init(bool isOffset)
    {
        // 保存原始颜色
        _originalColor = isOffset ? _offsetColor : _baseColor;
        _renderer.color = _originalColor;
    }

    // 供 GridManager 调用的高亮方法
    public void SetHighlight(bool isHighlighted)
    {
        if (_renderer == null) return;
        _renderer.color = isHighlighted ? _highlightColor : _originalColor;
    }
}
