using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FarmManager : MonoBehaviour
{
    public Tilemap farmTilemap;
    public Tile preparedTile;
    public Tile seedTile;

    private Dictionary<Vector3Int, TileState> tileStates = new Dictionary<Vector3Int, TileState>();

    public enum TileState { NotPrepared, Prepared, PlantedSeed }

    void Start()
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
}
