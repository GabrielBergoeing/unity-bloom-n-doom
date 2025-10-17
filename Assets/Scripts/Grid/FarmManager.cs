using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FarmManager : MonoBehaviour
{
    //Singleton Call for all TileInteractions to access
    public static FarmManager instance;

    public Tilemap farmTilemap;
    public Tile preparedTile;
    public Tile seedTile;
    private Vector3Int lastHighlightedTile = Vector3Int.zero;

    private Dictionary<Vector3Int, TileState> tileStates = new Dictionary<Vector3Int, TileState>();

    public enum TileState { NotPrepared, Prepared, PlantedSeed }

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        BoundsInt bounds = farmTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (farmTilemap.HasTile(pos))
                tileStates[pos] = TileState.NotPrepared;
        }
    }

    public void PrepareTile(Vector3Int cellPos)
    {
        if (tileStates.ContainsKey(cellPos) && tileStates[cellPos] == TileState.NotPrepared)
        {
            farmTilemap.SetTile(cellPos, preparedTile);
            tileStates[cellPos] = TileState.Prepared;
        }
    }

    public void PlantSeed(Vector3Int cellPos)
    {
        if (tileStates.ContainsKey(cellPos) && tileStates[cellPos] == TileState.Prepared)
        {
            farmTilemap.SetTile(cellPos, seedTile);
            tileStates[cellPos] = TileState.PlantedSeed;
        }
    }

    public void HighlightTile(Vector3Int cellPos)
    {
        if (tileStates.ContainsKey(lastHighlightedTile))
        {
            switch (tileStates[lastHighlightedTile])
            {
                case TileState.NotPrepared:
                    farmTilemap.SetTile(lastHighlightedTile, null);
                    break;
                case TileState.Prepared:
                    farmTilemap.SetTile(lastHighlightedTile, preparedTile);
                    break;
                case TileState.PlantedSeed:
                    farmTilemap.SetTile(lastHighlightedTile, seedTile);
                    break;
            }
        }
    }

    public bool IsPrepared(Vector3Int cellPos)
    {
        return tileStates.ContainsKey(cellPos) && tileStates[cellPos] == TileState.Prepared;
    }
}
