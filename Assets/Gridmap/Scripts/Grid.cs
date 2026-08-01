using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
public class GridManager {
    [SerializeField] private int Width, Height;
    [SerializeField] private Tile _tilePrefab;

    private int[,] gridArray;
    public GridManager(int width, int height) {
        Width = width;
        Height = height;
        gridArray = new int[width, height];
    }
    void GenerateGrid()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                gridArray[x, y] = 0;
            }
        }
    }
}
