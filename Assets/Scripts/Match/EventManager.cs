using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EventManager : MonoBehaviour
{
    public static EventManager instance;
    private LevelData currentLevel;

    [Header("Spawnable Prefabs")]
    [SerializeField] private List<GameObject> mapSeeds;
    [SerializeField] private List<GameObject> mapTools;
    [SerializeField] private List<GameObject> mapRarities;

    [Header("Spawn Terrain")]
    [SerializeField] private Tilemap landTilemap;

    [Header("Spawn Rates (seconds)")]
    [SerializeField] private float seedSpawnInterval = 10f;
    [SerializeField] private float toolSpawnInterval = 18f;
    [SerializeField] private float raritySpawnInterval = 45f;

    private float seedTimer;
    private float toolTimer;
    private float rarityTimer;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        currentLevel = GameManager.instance.currentLevel;
        mapSeeds = currentLevel.seedPrefabs;
        mapTools = currentLevel.toolPrefabs;
        mapRarities = currentLevel.rarePrefabs;
    }

    private void Update()
    {
        seedTimer += Time.deltaTime;
        toolTimer += Time.deltaTime;
        rarityTimer += Time.deltaTime;

        if (seedTimer >= seedSpawnInterval)
        {
            SpawnRandomItem(mapSeeds);
            seedTimer = 0f;
        }
        if (toolTimer >= toolSpawnInterval)
        {
            SpawnRandomItem(mapTools);
            toolTimer = 0f;
        }
        if (rarityTimer >= raritySpawnInterval)
        {
            SpawnRandomItem(mapRarities);
            rarityTimer = 0f;
        }
    }

    private void SpawnRandomItem(List<GameObject> itemList)
    {
        if (itemList.Count == 0) return;

        Vector3? spawnPos = GetRandomFreeTile();

        if (spawnPos == null) 
            return;

        GameObject prefab = itemList[Random.Range(0, itemList.Count)];
        Instantiate(prefab, spawnPos.Value, Quaternion.identity);
    }

    private Vector3? GetRandomFreeTile()
    {
        BoundsInt bounds = landTilemap.cellBounds;
        int attempts = 50; // safety to prevent infinite loops

        while (attempts-- > 0)
        {
            int x = Random.Range(bounds.xMin, bounds.xMax);
            int y = Random.Range(bounds.yMin, bounds.yMax);
            Vector3Int cell = new Vector3Int(x, y, 0);

            // Tile must exist (walkable zone)
            if (!landTilemap.HasTile(cell))
                continue;

            Vector3 worldPos = landTilemap.GetCellCenterWorld(cell);

            // Ensure nothing else is here
            Collider2D hit = Physics2D.OverlapCircle(worldPos, 0.25f);
            if (hit == null)
                return worldPos;
        }

        return null; // no free position found
    }
}
