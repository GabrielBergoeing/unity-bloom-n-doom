using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry of every prefab/asset the network layer needs to resolve by a stable id.
/// Ids are simply list indices, which are identical on every peer because the asset
/// is shared. Lives at Assets/Resources/NetworkAssets.asset (created and populated
/// by Tools > NGO Setup > Run Full Setup).
/// </summary>
public class NetworkAssets : ScriptableObject
{
    private static NetworkAssets _instance;
    public static NetworkAssets Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<NetworkAssets>("NetworkAssets");
            return _instance;
        }
    }

    [Header("Selectable characters (index == characterId)")]
    public CharacterDatabase characterDatabase;

    [Header("Playable levels (index == levelId)")]
    public List<LevelData> levels = new();

    [Header("World pickup prefabs (index == itemId)")]
    public List<GameObject> items = new();

    [Header("Plant prefabs (server spawned)")]
    public List<GameObject> plants = new();

    [Header("Projectile prefabs (index == projectileId, not networked)")]
    public List<GameObject> projectiles = new();

    [Header("Session prefab (spawned by the host)")]
    public GameObject sessionPrefab;

    public CharacterData[] Characters =>
        characterDatabase != null ? characterDatabase.characters : new CharacterData[0];

    public LevelData GetLevel(int index) =>
        index >= 0 && index < levels.Count ? levels[index] : null;

    public GameObject GetItemPrefab(int itemId) =>
        itemId >= 0 && itemId < items.Count ? items[itemId] : null;

    public int ItemIdOf(GameObject prefab) => items.IndexOf(prefab);

    public GameObject GetProjectilePrefab(int id) =>
        id >= 0 && id < projectiles.Count ? projectiles[id] : null;

    public int ProjectileIdOf(GameObject prefab) => projectiles.IndexOf(prefab);
}
