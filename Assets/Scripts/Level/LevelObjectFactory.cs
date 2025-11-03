using UnityEngine;

public class LevelObjectFactory : MonoBehaviour
{
    public TilePlacementHandler wallHandler;
    public TilePlacementHandler landHandler;
    public TilePlacementHandler waterHandler;

    public void Create(LevelObjectType type, Vector3 position, string variant)
    {
        Vector3Int cell = Vector3Int.FloorToInt(position);

        switch(type)
        {
            case LevelObjectType.Wall:
                wallHandler?.PlaceTile(cell, variant);
                break;

            case LevelObjectType.Land:
                landHandler?.PlaceTile(cell, variant);
                break;

            case LevelObjectType.Water:
                waterHandler?.PlaceTile(cell, variant);
                break;
        }
    }
}

