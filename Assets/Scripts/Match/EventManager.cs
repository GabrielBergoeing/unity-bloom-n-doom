using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    [Header("Spawnable Prefabs")]
    [SerializeField] private List<GameObject> mapSeeds;
    [SerializeField] private List<GameObject> mapTools;
    [SerializeField] private List<GameObject> mapRarities;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> spawnPoints;

    [Header("Spawn Rates (seconds)")]
    [Tooltip("Time between seed spawns")]
    [SerializeField] private float seedSpawnInterval = 10f;

    [Tooltip("Time between tool spawns")]
    [SerializeField] private float toolSpawnInterval = 18f;

    [Tooltip("Time between rare item spawns")]
    [SerializeField] private float raritySpawnInterval = 45f;

    private float seedTimer;
    private float toolTimer;
    private float rarityTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        if (itemList.Count == 0 || spawnPoints.Count == 0) return;

        Transform spawn = GetRandomFreeSpawnPoint();
        if (spawn == null) return;

        GameObject prefab = itemList[Random.Range(0, itemList.Count)];
        Instantiate(prefab, spawn.position, Quaternion.identity);
    }

    private Transform GetRandomFreeSpawnPoint()
    {
        List<Transform> shuffledPoints = new(spawnPoints);
        Shuffle(shuffledPoints);

        foreach (var point in shuffledPoints)
        {
            // If nothing is in this tile (you can use tags, colliders, tilemap check, etc.)
            Collider2D hit = Physics2D.OverlapCircle(point.position, 0.2f);
            if (hit == null)
                return point;
        }
        return null;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }
}
