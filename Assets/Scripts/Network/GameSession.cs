using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One entry per connected player in the pre-match lobby.
/// </summary>
public struct LobbyPlayer : INetworkSerializable, System.IEquatable<LobbyPlayer>
{
    public ulong clientId;
    public int characterId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref characterId);
    }

    public bool Equals(LobbyPlayer other) =>
        clientId == other.clientId && characterId == other.characterId;
}

/// <summary>
/// Server-spawned session object (persists across scene loads) that owns:
///  - the lobby (connected players + character selection),
///  - the match flow (level selection -> networked scene load -> player spawning -> timer -> results),
///  - server-authoritative relays for farm actions, item pickups/drops and projectiles.
/// </summary>
public class GameSession : NetworkBehaviour
{
    public static GameSession Instance { get; private set; }

    /// <summary>True whenever an online session is running on this peer.</summary>
    public static bool OnlineActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public enum SessionState { Lobby = 0, Loading = 1, Playing = 2, Results = 3 }

    public const string LevelSceneName = "LevelScene";

    public NetworkList<LobbyPlayer> lobbyPlayers = new NetworkList<LobbyPlayer>();
    public NetworkVariable<int> levelIndex = new(0);
    public NetworkVariable<float> matchTimer = new(0f);
    public NetworkVariable<int> state = new((int)SessionState.Lobby);

    public SessionState State => (SessionState)state.Value;

    // --- server only ---
    private readonly HashSet<ulong> levelReadyClients = new();
    private float loadingTimeout;
    private bool sceneLoadStarted;
    private bool resultsSent;

    // ======================================================
    //  LIFECYCLE
    // ======================================================
    public override void OnNetworkSpawn()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Every peer (host and clients) tracks this, not just the server - TelemetryLogger
        // captures per-machine FPS/CPU/GPU and needs to run on everyone during a match.
        state.OnValueChanged += OnSessionStateChanged;

        if (IsServer)
        {
            foreach (var id in NetworkManager.ConnectedClientsIds)
                ServerAddLobbyPlayer(id);

            NetworkManager.OnConnectionEvent += OnConnectionEvent;
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        }
    }

    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= OnSessionStateChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnConnectionEvent -= OnConnectionEvent;
            if (NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
        if (Instance == this)
            Instance = null;
    }

    private void OnSessionStateChanged(int previous, int current)
    {
        if ((SessionState)current == SessionState.Playing)
        {
            if (TelemetryLogger.instance == null)
                new GameObject("TelemetryLogger").AddComponent<TelemetryLogger>();
            TelemetryLogger.instance.StartCapture();
        }
        else if ((SessionState)previous == SessionState.Playing)
        {
            TelemetryLogger.instance?.StopCapture();
        }
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer) return;

        if (State == SessionState.Loading && !sceneLoadStarted)
        {
            if (AllClientsReady() || Time.unscaledTime > loadingTimeout)
            {
                sceneLoadStarted = true;
                NetworkManager.SceneManager.LoadScene(LevelSceneName, LoadSceneMode.Single);
            }
        }
        else if (State == SessionState.Playing)
        {
            matchTimer.Value -= Time.deltaTime;
            if (matchTimer.Value <= 0f)
                ServerEndMatch();
        }
    }

    // ======================================================
    //  LOBBY (server)
    // ======================================================
    private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
    {
        if (!IsServer) return;

        if (data.EventType == ConnectionEvent.ClientConnected)
            ServerAddLobbyPlayer(data.ClientId);
        else if (data.EventType == ConnectionEvent.ClientDisconnected)
            ServerRemoveLobbyPlayer(data.ClientId);
    }

    private void ServerAddLobbyPlayer(ulong clientId)
    {
        for (int i = 0; i < lobbyPlayers.Count; i++)
            if (lobbyPlayers[i].clientId == clientId) return;

        lobbyPlayers.Add(new LobbyPlayer
        {
            clientId = clientId,
            characterId = FirstFreeCharacterId()
        });
    }

    private void ServerRemoveLobbyPlayer(ulong clientId)
    {
        for (int i = lobbyPlayers.Count - 1; i >= 0; i--)
            if (lobbyPlayers[i].clientId == clientId)
                lobbyPlayers.RemoveAt(i);
    }

    private int FirstFreeCharacterId()
    {
        int count = NetworkAssets.Instance.Characters.Length;
        for (int id = 0; id < count; id++)
        {
            bool taken = false;
            for (int i = 0; i < lobbyPlayers.Count; i++)
                if (lobbyPlayers[i].characterId == id) { taken = true; break; }
            if (!taken) return id;
        }
        return 0;
    }

    public int GetLobbyCharacterId(ulong clientId)
    {
        for (int i = 0; i < lobbyPlayers.Count; i++)
            if (lobbyPlayers[i].clientId == clientId)
                return lobbyPlayers[i].characterId;
        return 0;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SelectCharacterServerRpc(int characterId, ServerRpcParams rpcParams = default)
    {
        if (State != SessionState.Lobby) return;

        int count = NetworkAssets.Instance.Characters.Length;
        if (count == 0) return;
        characterId = ((characterId % count) + count) % count;

        ulong sender = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].clientId == sender)
            {
                lobbyPlayers[i] = new LobbyPlayer { clientId = sender, characterId = characterId };
                return;
            }
        }
    }

    // ======================================================
    //  MATCH START (host)
    // ======================================================
    public void HostStartMatch(int levelIdx)
    {
        if (!IsServer || State != SessionState.Lobby) return;
        if (NetworkAssets.Instance.GetLevel(levelIdx) == null) return;

        levelIndex.Value = levelIdx;
        state.Value = (int)SessionState.Loading;
        levelReadyClients.Clear();
        sceneLoadStarted = false;
        loadingTimeout = Time.unscaledTime + 10f;

        PrepareLevelClientRpc(levelIdx);
    }

    [ClientRpc]
    private void PrepareLevelClientRpc(int levelIdx)
    {
        var level = NetworkAssets.Instance.GetLevel(levelIdx);
        if (level == null) return;

        if (GameManager.instance != null)
            GameManager.instance.currentLevel = level;

        if (AudioManager.instance != null)
        {
            AudioManager.instance.StopBGM();
            if (!string.IsNullOrEmpty(level.bgmTrackName))
                AudioManager.instance.StartBGM(level.bgmTrackName);
        }

        LevelPreparedServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void LevelPreparedServerRpc(ServerRpcParams rpcParams = default)
    {
        levelReadyClients.Add(rpcParams.Receive.SenderClientId);
    }

    private bool AllClientsReady()
    {
        foreach (var id in NetworkManager.ConnectedClientsIds)
            if (!levelReadyClients.Contains(id))
                return false;
        return true;
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (!IsServer) return;
        if (sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted) return;
        if (sceneEvent.SceneName != LevelSceneName) return;

        SpawnPlayersAndStartMatch();
    }

    private void SpawnPlayersAndStartMatch()
    {
        var assets = NetworkAssets.Instance;
        var level = assets.GetLevel(levelIndex.Value);
        var characters = assets.Characters;

        int i = 0;
        foreach (var lp in lobbyPlayers)
        {
            var charData = characters[Mathf.Clamp(lp.characterId, 0, characters.Length - 1)];
            Vector3 pos = level != null && i < level.playerSpawnPositions.Length
                ? (Vector3)level.playerSpawnPositions[i]
                : new Vector3(i * 2f, 0f, 0f);

            var go = Instantiate(charData.prefab, pos, Quaternion.identity);
            var np = go.GetComponent<NetworkPlayer>();
            if (np != null)
                np.ServerInit(i, lp.characterId);

            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(lp.clientId, true);
            i++;
        }

        matchTimer.Value = level != null ? level.matchDuration : 300f;
        resultsSent = false;
        state.Value = (int)SessionState.Playing;

        if (level != null && level.setTestFormat && FindFirstObjectByType<StressTestSetup>() == null)
            new GameObject("StressTestModule").AddComponent<StressTestSetup>();
    }

    // ======================================================
    //  MATCH END (server)
    // ======================================================
    private void ServerEndMatch()
    {
        if (resultsSent) return;
        resultsSent = true;

        state.Value = (int)SessionState.Results;

        var results = ScoreTally.ComputeNetworkResults();
        ShowResultsClientRpc(results.ToArray());
    }

    [ClientRpc]
    private void ShowResultsClientRpc(ScoreResult[] results)
    {
        MatchManager.instance?.ShowResultsLocal(new List<ScoreResult>(results));
    }

    // ======================================================
    //  FARM RELAYS
    //  Owner client -> ServerRpc (validate) -> ClientRpc (mirror on all peers)
    // ======================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestPrepareTileServerRpc(Vector3Int cell) => ServerTryPrepareTile(cell);

    /// <summary>Server-side prepare, shared by the owner-client relay above and by
    /// StressTestSetup (which runs directly on the server, no RPC hop needed).</summary>
    public bool ServerTryPrepareTile(Vector3Int cell)
    {
        if (!IsServer) return false;
        var farm = FarmManager.instance;
        if (farm == null || !farm.CanPrepareServer(cell)) return false;

        ApplyPrepareTileClientRpc(cell);
        return true;
    }

    [ClientRpc]
    private void ApplyPrepareTileClientRpc(Vector3Int cell) =>
        FarmManager.instance?.ApplyPrepareTile(cell);

    [ServerRpc(RequireOwnership = false)]
    public void RequestPlantSeedServerRpc(Vector3Int cell, int itemId, int ownerIndex)
    {
        var itemPrefab = NetworkAssets.Instance.GetItemPrefab(itemId);
        var seed = itemPrefab != null ? itemPrefab.GetComponent<Seed>() : null;
        if (seed == null || seed.PlantPrefab == null) return;

        ServerTryPlantSeed(cell, seed.PlantPrefab, ownerIndex);
    }

    /// <summary>Server-side plant, shared by the owner-client relay above and by
    /// StressTestSetup (which runs directly on the server, no RPC hop needed).</summary>
    public bool ServerTryPlantSeed(Vector3Int cell, GameObject plantPrefab, int ownerIndex)
    {
        if (!IsServer || plantPrefab == null) return false;
        var farm = FarmManager.instance;
        if (farm == null || !farm.IsPrepared(cell) || farm.IsOccupied(cell)) return false;

        ApplySeedTileClientRpc(cell);

        Vector3 worldPos = farm.farmTilemap.GetCellCenterWorld(cell);
        var plantGo = Instantiate(plantPrefab, worldPos, Quaternion.identity);
        var plant = plantGo.GetComponent<Plant>();
        plant.ServerPrepareInit(ownerIndex, cell);
        plantGo.GetComponent<NetworkObject>().Spawn(true);
        plant.InitClientRpc(ownerIndex, cell);
        return true;
    }

    [ClientRpc]
    private void ApplySeedTileClientRpc(Vector3Int cell) =>
        FarmManager.instance?.ApplySeedTile(cell);

    [ServerRpc(RequireOwnership = false)]
    public void RequestIrrigateServerRpc(Vector3Int cell) =>
        FarmManager.instance?.ServerIrrigate(cell);

    [ServerRpc(RequireOwnership = false)]
    public void RequestFertilizeServerRpc(Vector3Int cell) =>
        FarmManager.instance?.ServerFertilize(cell);

    [ServerRpc(RequireOwnership = false)]
    public void RequestRemovePlantServerRpc(Vector3Int cell, int requesterIndex, bool sabotage)
    {
        var farm = FarmManager.instance;
        if (farm == null) return;

        var plant = farm.TryGetPlant(cell);
        if (plant == null) return;

        // Removing your own plant requires ownership; sabotage (scissors) works on any plant.
        if (!sabotage && plant.ownerPlayerIndex != requesterIndex) return;

        ServerDespawnPlant(plant);
    }

    /// <summary>Server-side removal used by remove/sabotage requests and plant death.</summary>
    public void ServerDespawnPlant(Plant plant)
    {
        if (!IsServer || plant == null) return;

        ApplyClearTileClientRpc(plant.cellPos);

        var no = plant.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
        else
            Destroy(plant.gameObject);
    }

    [ClientRpc]
    private void ApplyClearTileClientRpc(Vector3Int cell) =>
        FarmManager.instance?.ApplyClearTile(cell);

    // ======================================================
    //  ITEM RELAYS
    // ======================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestPickupServerRpc(ulong itemNetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var itemObj))
            return;

        var pickup = itemObj.GetComponent<Pickup>();
        if (pickup == null) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        var playerObj = NetworkManager.ConnectedClients.TryGetValue(sender, out var cc)
            ? cc.PlayerObject : null;
        if (playerObj == null) return;

        if (Vector2.Distance(playerObj.transform.position, itemObj.transform.position) > 3f)
            return;

        int itemId = pickup.itemId;
        itemObj.Despawn(true);

        GrantPickupClientRpc(itemId, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } }
        });
    }

    [ClientRpc]
    private void GrantPickupClientRpc(int itemId, ClientRpcParams rpcParams = default)
    {
        NetworkPlayer.LocalPlayer?.ReceiveGrantedItem(itemId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSpawnItemServerRpc(int itemId, Vector3 position) =>
        ServerSpawnWorldItem(itemId, position);

    public void ServerSpawnWorldItem(int itemId, Vector3 position)
    {
        if (!IsServer) return;

        var prefab = NetworkAssets.Instance.GetItemPrefab(itemId);
        if (prefab == null) return;

        var go = Instantiate(prefab, position, Quaternion.identity);
        go.GetComponent<NetworkObject>().Spawn(true);
    }

    // ======================================================
    //  PROJECTILE RELAYS
    //  Gameplay effects are simulated only on the server; every peer
    //  spawns a local, visual-only copy so shots are visible everywhere.
    // ======================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestProjectileServerRpc(int projectileId, Vector3 position,
                                           Quaternion rotation, Vector2 inheritedVelocity)
    {
        SpawnProjectileLocal(projectileId, position, rotation, inheritedVelocity, authoritative: true);
        SpawnProjectileClientRpc(projectileId, position, rotation, inheritedVelocity);
    }

    [ClientRpc]
    private void SpawnProjectileClientRpc(int projectileId, Vector3 position,
                                          Quaternion rotation, Vector2 inheritedVelocity)
    {
        if (IsServer) return; // the host already simulated the authoritative copy
        SpawnProjectileLocal(projectileId, position, rotation, inheritedVelocity, authoritative: false);
    }

    private void SpawnProjectileLocal(int projectileId, Vector3 position, Quaternion rotation,
                                      Vector2 inheritedVelocity, bool authoritative)
    {
        var prefab = NetworkAssets.Instance.GetProjectilePrefab(projectileId);
        if (prefab == null) return;

        var go = Instantiate(prefab, position, rotation);

        var fire = go.GetComponent<Fire>();
        if (fire != null)
        {
            fire.SetInheritedVelocity(inheritedVelocity);
            fire.authoritative = authoritative;
        }

        var water = go.GetComponent<Watergun>();
        if (water != null)
        {
            water.SetInheritedVelocity(inheritedVelocity);
            water.authoritative = authoritative;
        }
    }
}
