using UnityEngine;

public class Tile : MonoBehaviour
{
    [SerializeField] private Color _baseColor, _offsetColor;
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private GameObject _highlight;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }
    public void Init(bool isOffset)
    {
        _renderer.color = isOffset ? _offsetColor : _baseColor;
    }

    void OnMouseEnter()
    {
        Debug.Log("鼠标进入瓦片：" + gameObject.name);
        _highlight.SetActive(true);
    }
    void OnMouseExit()
    {
        Debug.Log("鼠标离开瓦片：" + gameObject.name);
        _highlight.SetActive(false);
    }
}
