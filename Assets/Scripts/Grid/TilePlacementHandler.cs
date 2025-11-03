using UnityEngine;
using UnityEngine.Tilemaps;
using System;
using System.Collections.Generic;

public class TilePlacementHandler : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;

    [Header("Tile Type")]
    public LevelObjectType currentType;

    [Header("Tiles by Variant")]
    public List<EnumTilePair> tiles = new List<EnumTilePair>();

    [Serializable]
    public class EnumTilePair
    {
        public string variant;
        public TileBase tile;
    }

    private Dictionary<string, TileBase> tileLookup;

    private void Awake()
    {
        if (tilemap == null)
            tilemap = GetComponentInChildren<Tilemap>();

        tileLookup = new Dictionary<string, TileBase>();
        foreach (var t in tiles)
        {
            if (!tileLookup.ContainsKey(t.variant))
                tileLookup.Add(t.variant, t.tile);
        }
    }

    public void PlaceTile(Vector3Int cell, string subtype)
    {
        if (tileLookup.TryGetValue(subtype, out var tile))
            tilemap.SetTile(cell, tile);
        else
            Debug.LogWarning($"❌ No tile assigned for {currentType}.{subtype}");
    }

    public void ClearTile(Vector3Int cell)
    {
        tilemap.SetTile(cell, null);
    }
}
