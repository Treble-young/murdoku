using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    [SerializeField] private int Width, Height;
    [SerializeField] private Tile _tilePrefab;
    [SerializeField] private Transform _cam;

    // 用于记录当前高亮的 Tile，避免重复刷新
    private Tile _currentHighlightedTile;

    private Dictionary<Vector2, Tile> Tiles;
    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        Tiles = new Dictionary<Vector2, Tile>();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var spawnedTile = Instantiate(_tilePrefab, new Vector3(x, y), Quaternion.identity);
                spawnedTile.name = $"Tile {x} {y}";

                var isOffset = (x % 2 == 0 && y % 2 != 0) || (x % 2 != 0 && y % 2 == 0);
                spawnedTile.Init(isOffset);

                Tiles[new Vector2(x, y)] = spawnedTile;
            }
        }
        _cam.transform.position = new Vector3((float)Width / 2 - 0.5f, (float)Height / 2 - 0.5f, -10);
    }

    public Tile GetTileAtPosition(Vector2 pos)
    {
        if(Tiles.TryGetValue(pos,out var tile))
        {
            return tile;
        }
        return null;
    }

    void Update()
    {
        // 1. 从摄像机向鼠标位置发射一条 2D 射线
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 rayOrigin = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

        // 2. 进行 2D 射线检测（只检测鼠标位置的那个点）
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.zero);

        // 3. 判断是否击中了带有 Tile 脚本的对象
        if (hit.collider != null)
        {
            Tile tile = hit.collider.GetComponent<Tile>();
            if (tile != null)
            {
                // 如果击中的 Tile 和当前高亮的不同，更新高亮
                if (_currentHighlightedTile != tile)
                {
                    // 取消旧的高亮
                    if (_currentHighlightedTile != null)
                        _currentHighlightedTile.SetHighlight(false);

                    // 设置新的高亮
                    _currentHighlightedTile = tile;
                    _currentHighlightedTile.SetHighlight(true);
                }
                return; // 命中后直接返回，不执行下面的清除逻辑
            }
        }

        // 4. 如果鼠标没有指向任何 Tile，清除现有高亮
        if (_currentHighlightedTile != null)
        {
            _currentHighlightedTile.SetHighlight(false);
            _currentHighlightedTile = null;
        }
    }
}
