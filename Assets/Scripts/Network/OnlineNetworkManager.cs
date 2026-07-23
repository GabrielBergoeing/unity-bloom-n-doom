using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

// Client -> server request to pick a level. Any connected client can send this (not just
// a P2P host, which happened to have local server authority by accident of running
// server+client in the same process) - a true dedicated server (GameLift) never has a
// "host player" with that authority, so the request has to travel over the network like
// any other client action instead of being called directly from the clicking client's own
// process. Top-level (not nested in a class) so both OnlineNetworkManager and
// UI_MapSelectorOnline reference the exact same Mirror message type - a nested struct
// with the same name in each class would be two distinct types as far as Mirror's
// message-type identification is concerned, and messages sent as one are silently never
// delivered to a handler registered for the other.
public struct LevelSelectRequestMessage : NetworkMessage { public int levelIndex; public string levelSceneName; }

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

    // Exposed so OnlineMatchManager can gate isMatchRunning on the full expected roster
    // having registered, rather than firing the instant the first Player.OnStartServer()
    // happens to land. Character-select scene sets this via StoreConnectionSlots before
    // ServerChangeScene; the gameplay scene reads it back once OnlineMatchManager spawns.
    public int ConnectionSlotCount => connectionSlots.Count;

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

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<LevelSelectRequestMessage>(OnLevelSelectRequested, false);
    }

    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<LevelSelectRequestMessage>();
        base.OnStopServer();
    }

    private void OnLevelSelectRequested(NetworkConnectionToClient conn, LevelSelectRequestMessage msg)
    {
        SelectLevel(msg.levelIndex, msg.levelSceneName);
    }

    // SteamLobby's connect-with-fallback retry (JoinWithFallback) needs a reliable
    // "did this attempt actually succeed" signal. NetworkClient.OnConnectedEvent/
    // OnDisconnectedEvent can't be used for that - NetworkManager.StartClient()
    // overwrites them with `=` (not `+=`) on every call, silently dropping any outside
    // subscriber. These virtual methods are Mirror's intended extension point instead:
    // OnClientConnect only fires after authentication succeeds, and isn't clobbered.
    public event Action ClientConnectedOnline;
    public event Action ClientDisconnectedOnline;

    // Tracks whether THIS connection attempt ever actually succeeded, so
    // OnClientDisconnect can tell "we were in a real match and it died" apart from
    // "SteamLobby's LAN -> public IP -> hole punch retry just tore down a failed tier
    // to try the next one" - both call StopClient() and land here, but only the first
    // should send everyone back to the main menu.
    private bool hasBeenConnected;

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        hasBeenConnected = true;
        ClientConnectedOnline?.Invoke();
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        ClientDisconnectedOnline?.Invoke();

        bool wasReallyConnected = hasBeenConnected;
        hasBeenConnected = false;

        // Fires for every disconnect, voluntary or not (Mirror funnels host-closed-lobby,
        // dropped connection, and our own StopHost()/StopClient() calls through here alike -
        // see NetworkManager.StopClient's own comment on this). Without this, only whichever
        // player manually clicks "return to menu" leaves the (now-dead) session; everyone
        // else is stuck looking at a scene whose server no longer exists. Gated on
        // wasReallyConnected so a failed retry tier (which never reached OnClientConnect)
        // doesn't bounce the player mid-cascade.
        if (wasReallyConnected && GameManager.instance != null)
            GameManager.instance.ChangeScene("MainMenu");
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

        Vector3 spawnPos = GetOnlineSpawnPosition(slotIndex, conn.connectionId);
        GameObject player = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

        // Set pre-spawn so the SyncVar is included in the initial spawn message.
        // Cannot use [Server]-attributed methods here since isServer is false before spawn.
        Player playerComp = player.GetComponent<Player>();
        if (playerComp != null)
            playerComp.onlinePlayerIndex = slotIndex;

        NetworkServer.AddPlayerForConnection(conn, player);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────
    private Vector3 GetOnlineSpawnPosition(int slotIndex, int connectionId)
    {
        LevelData level = GameManager.instance?.currentLevel;

        if (level?.playerSpawnPositions != null &&
            slotIndex >= 0 && slotIndex < level.playerSpawnPositions.Length)
        {
            return level.playerSpawnPositions[slotIndex];
        }

        // slotIndex is -1 for any connection not in connectionSlots (joined/reconnected after
        // StoreConnectionSlots already ran) - fall back to connectionId instead of the raw
        // slotIndex so each unmapped player still gets spread across distinct start positions
        // rather than everyone landing on the exact same spot. connectionId is always >= 0,
        // slotIndex is not (C#'s % returns a negative result for a negative dividend, e.g.
        // -1 % 4 == -1, which would throw indexing a List<Transform>), so use safe modulo too.
        int fallbackIndex = slotIndex >= 0 ? slotIndex : connectionId;

        if (startPositions.Count > 0)
        {
            int safeIndex = ((fallbackIndex % startPositions.Count) + startPositions.Count) % startPositions.Count;
            return startPositions[safeIndex].position;
        }

        return new Vector3(fallbackIndex * 2f, 0f, 0f);
    }

    private void OnClientLevelSelected(LevelSelectedMessage msg)
    {
        LevelData chosen = GetLevel(msg.levelIndex);
        if (chosen != null && GameManager.instance != null)
        {
            GameManager.instance.currentLevel = chosen;
            Debug.Log($"[OnlineNetworkManager] Client received level: {chosen.name}");
        }

        // Every client (including a P2P host's own local loopback client) gets its BGM
        // switch from this single broadcast now - UI_MapSelectorOnline no longer applies
        // the selection or switches audio directly, since only a true dedicated server
        // process can do that, and under GameLift no clicking player's process is ever
        // the server.
        if (chosen != null && AudioManager.instance != null)
        {
            AudioManager.instance.StopBGM();
            AudioManager.instance.StartBGM(chosen.bgmTrackName);
        }
    }
}