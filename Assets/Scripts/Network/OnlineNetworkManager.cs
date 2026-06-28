using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Drop-in replacement for NetworkManager that handles online-specific needs:
///   • Spawns the correct character prefab per connection in OnServerAddPlayer.
///   • Syncs the selected LevelData to all clients before scene change.
///
/// Setup: replace the NetworkManager component on your Network Manager prefab
/// with this component (it extends NetworkManager so nothing else breaks).
/// Assign the Levels list to match UI_MapSelectorOnline's list order.
/// </summary>
public class OnlineNetworkManager : NetworkManager
{
    [Header("Online — Levels (must match MapSelectorOnline order)")]
    [SerializeField] private List<LevelData> levels = new();

    // ── Connection → slot index map, filled by UI_MatchMenuOnline before scene change ──
    private readonly Dictionary<int, int> connectionSlots = new();

    // ── Level synced to clients via message ──
    private struct LevelSelectedMessage : NetworkMessage { public int levelIndex; }

    // ── Public accessors ──
    public LevelData GetLevel(int index) =>
        (index >= 0 && index < levels.Count) ? levels[index] : null;

    // ─────────────────────────────────────────────────────────────────
    //  Called by UI_MatchMenuOnline.TryStartMatch() BEFORE ServerChangeScene
    // ─────────────────────────────────────────────────────────────────
    public void StoreConnectionSlots(IReadOnlyList<int> orderedConnectionIds)
    {
        connectionSlots.Clear();
        for (int i = 0; i < orderedConnectionIds.Count; i++)
            connectionSlots[orderedConnectionIds[i]] = i;

        Debug.Log($"[OnlineNetworkManager] Stored {connectionSlots.Count} connection slots.");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Called by UI_MapSelectorOnline.SelectLevel()
    // ─────────────────────────────────────────────────────────────────
    public void SelectLevel(int index, string levelSceneName)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[OnlineNetworkManager] SelectLevel called on non-server.");
            return;
        }

        if (index < 0 || index >= levels.Count)
        {
            Debug.LogError($"[OnlineNetworkManager] Level index {index} out of range.");
            return;
        }

        LevelData chosen = levels[index];

        // Apply on host immediately
        if (GameManager.instance != null)
            GameManager.instance.currentLevel = chosen;

        // Broadcast level to all clients so they can set GameManager.currentLevel
        // before the scene loads (Mirror processes messages in send order)
        NetworkServer.SendToAll(new LevelSelectedMessage { levelIndex = index });

        Debug.Log($"[OnlineNetworkManager] SelectLevel {index} ({chosen.name}), changing to {levelSceneName}");
        ServerChangeScene(levelSceneName);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Mirror overrides
    // ─────────────────────────────────────────────────────────────────
    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<LevelSelectedMessage>(OnClientLevelSelected, false);
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<LevelSelectedMessage>();
        base.OnStopClient();
    }

    /// <summary>
    /// Spawns the character prefab that was chosen by the player owning this connection.
    /// Falls back to playerPrefab if no mapping exists.
    /// </summary>
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        int slotIndex = connectionSlots.TryGetValue(conn.connectionId, out int idx) ? idx : -1;

        var selections = PlayerInputService.instance?.OnlineSelectedCharacters;
        GameObject prefabToSpawn = playerPrefab;

        if (selections != null && slotIndex >= 0 && slotIndex < selections.Count)
        {
            CharacterData charData = selections[slotIndex];
            if (charData?.prefab != null)
            {
                prefabToSpawn = charData.prefab;
                Debug.Log($"[OnlineNetworkManager] Spawning {charData.characterName} for conn {conn.connectionId} (slot {slotIndex})");
            }
        }
        else
        {
            Debug.LogWarning($"[OnlineNetworkManager] No character data for conn {conn.connectionId} (slot {slotIndex}). Using playerPrefab.");
        }

        Vector3 spawnPos = GetOnlineSpawnPosition(slotIndex);
        GameObject player = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────
    private Vector3 GetOnlineSpawnPosition(int slotIndex)
    {
        LevelData level = GameManager.instance?.currentLevel;

        if (level?.playerSpawnPositions != null &&
            slotIndex >= 0 && slotIndex < level.playerSpawnPositions.Length)
        {
            return level.playerSpawnPositions[slotIndex];
        }

        // Fallback: use Mirror's registered start positions
        if (startPositions.Count > 0)
            return startPositions[slotIndex % startPositions.Count].position;

        return new Vector3(slotIndex * 2f, 0f, 0f);
    }

    private void OnClientLevelSelected(LevelSelectedMessage msg)
    {
        LevelData chosen = GetLevel(msg.levelIndex);
        if (chosen != null && GameManager.instance != null)
        {
            GameManager.instance.currentLevel = chosen;
            Debug.Log($"[OnlineNetworkManager] Client received level: {chosen.name}");
        }
    }
}
