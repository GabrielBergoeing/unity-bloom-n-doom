using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "Game/Level Data", order = 1)]
public class LevelData : ScriptableObject
{
    [Header("Level Layout (.json reference)")]
    public string jsonFileName;

    [Header("Match Settings")]
    [Tooltip("Match duration in seconds.")]
    public float matchDuration = 900f;

    [Header("Player Spawn Positions (Max 4 Players)")]
    public Vector2[] playerSpawnPositions = new Vector2[4];

    [Header("Level BGM")]
    public string bgmTrackName;

    [Header("Spawnable Prefabs")]
    public List<GameObject> seedPrefabs = new();
    public List<GameObject> toolPrefabs = new();
    public List<GameObject> rarePrefabs = new();
}
