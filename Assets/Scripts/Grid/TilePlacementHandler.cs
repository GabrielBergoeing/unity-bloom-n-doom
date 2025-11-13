using UnityEngine;
using UnityEngine.Tilemaps;
using System;
using System.Collections.Generic;

[ExecuteInEditMode]   // Allows levels to be edited on editor
public class TilePlacementHandler : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;
    public Tilemap Tilemap => tilemap;

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

    private void OnEnable()
    {
        if (tilemap == null)
            tilemap = GetComponentInChildren<Tilemap>();

        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    private void RebuildLookup()
    {
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
            Debug.LogWarning($"No tile assigned for {currentType}.{subtype}");
    }

    public void ClearTile(Vector3Int cell)
    {
        tilemap.SetTile(cell, null);
    }

    public bool TryGetSubtypeFromTile(TileBase tile, out string subtype)
    {
        foreach (var pair in tiles)
        {
            if (pair.tile == tile)
            {
                subtype = pair.variant;
                return true;
            }
        }

        subtype = null;
        return false;
    }
}
