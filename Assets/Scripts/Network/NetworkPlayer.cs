using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Network glue on every character prefab.
/// - Owner: enables input/camera/UI and pushes movement, facing, held item and water to the network.
/// - Remote copies: gameplay logic disabled, driven by ClientNetworkTransform / OwnerNetworkAnimator
///   plus the small state mirrored here (facing, held item, water).
/// Also forwards all owner gameplay requests to the GameSession relays.
/// </summary>
[RequireComponent(typeof(Player))]
public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer LocalPlayer { get; private set; }
    public static readonly List<NetworkPlayer> All = new();

    public NetworkVariable<int> playerIndex = new(-1);
    public NetworkVariable<int> characterId = new(0);

    public NetworkVariable<int> heldItemId = new(-1,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> netWaterSupply = new(100f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Vector2> netMove = new(Vector2.zero,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Vector2> netFacing = new(new Vector2(0f, 1f),
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public Player player { get; private set; }
    public int Index => playerIndex.Value;

    private GameObject remoteHandVisual;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    /// <summary>Called by the server right before Spawn().</summary>
    public void ServerInit(int index, int charId)
    {
        playerIndex.Value = index;
        characterId.Value = charId;
    }

    // ======================================================
    //  SPAWN / DESPAWN
    // ======================================================
    public override void OnNetworkSpawn()
    {
        All.Add(this);
        gameObject.name = $"Player_{playerIndex.Value}{(IsOwner ? "_Local" : "_Remote")}";

        if (IsOwner)
        {
            LocalPlayer = this;
            SetupOwner();
        }
        else
        {
            SetupRemote();
        }

        heldItemId.OnValueChanged += OnHeldItemChanged;
        if (!IsOwner && heldItemId.Value >= 0)
            RefreshRemoteHand(heldItemId.Value);
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
        if (LocalPlayer == this)
            LocalPlayer = null;

        heldItemId.OnValueChanged -= OnHeldItemChanged;
        if (remoteHandVisual != null)
            Destroy(remoteHandVisual);
    }

    private void SetupOwner()
    {
        var pi = GetComponent<PlayerInput>();
        if (pi != null)
        {
            pi.enabled = true;
            if (pi.currentActionMap == null || pi.currentActionMap.name != "Player")
                pi.SwitchCurrentActionMap("Player");
        }

        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.DisableJoining();

        player.SetControl(true);

        var cam = GetComponentInChildren<Player_ScreenCamera>(true);
        if (cam != null) cam.ConfigureAsLocal();

        var hotbarUI = FindFirstObjectByType<UI_Hotbar>();
        if (hotbarUI != null && player.inventory != null)
            hotbarUI.AssignHotbar(player.inventory);
    }

    private void SetupRemote()
    {
        var pi = GetComponent<PlayerInput>();
        if (pi != null) pi.enabled = false;

        var hotbar = GetComponent<HotbarSystem>();
        if (hotbar != null) hotbar.enabled = false;

        var tile = GetComponentInChildren<TileInteraction>(true);
        if (tile != null) tile.enabled = false;

        var cam = GetComponentInChildren<Player_ScreenCamera>(true);
        if (cam != null) cam.DisableAsRemote();

        // No duplicated per-player overlay UI (timer, water bar) from remote copies.
        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            canvas.gameObject.SetActive(false);

        if (player.rb != null)
            player.rb.bodyType = RigidbodyType2D.Kinematic;

        player.SetControl(false);
    }

    // ======================================================
    //  STATE MIRRORING
    // ======================================================
    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            if (netMove.Value != player.moveInput)
                netMove.Value = player.moveInput;

            var facing = new Vector2(player.xFacingDir, player.yFacingDir);
            if (netFacing.Value != facing)
                netFacing.Value = facing;

            if (!Mathf.Approximately(netWaterSupply.Value, player.waterSupply))
                netWaterSupply.Value = player.waterSupply;
        }
        else
        {
            player.ApplyRemoteState(netMove.Value,
                (int)netFacing.Value.x, (int)netFacing.Value.y);
            player.waterSupply = netWaterSupply.Value;
        }
    }

    // ======================================================
    //  HELD ITEM (remote hand visual)
    // ======================================================
    public void SetHeldItem(int itemId)
    {
        if (IsSpawned && IsOwner && heldItemId.Value != itemId)
            heldItemId.Value = itemId;
    }

    private void OnHeldItemChanged(int oldId, int newId)
    {
        if (IsOwner) return; // the owner has the real hand instance from its hotbar
        RefreshRemoteHand(newId);
    }

    private void RefreshRemoteHand(int itemId)
    {
        if (remoteHandVisual != null)
        {
            Destroy(remoteHandVisual);
            remoteHandVisual = null;
        }

        if (itemId < 0) return;

        var prefab = NetworkAssets.Instance != null ? NetworkAssets.Instance.GetItemPrefab(itemId) : null;
        if (prefab == null) return;

        Transform hand = transform.Find("OnHand");
        if (hand == null) return;

        remoteHandVisual = Instantiate(prefab, hand);
        remoteHandVisual.transform.localPosition = Vector3.zero;
        remoteHandVisual.transform.localRotation = Quaternion.identity;
        MakeVisualOnly(remoteHandVisual);
    }

    private static void MakeVisualOnly(GameObject go)
    {
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        var pickup = go.GetComponent<Pickup>();
        if (pickup != null)
        {
            pickup.isPickedUp = true;
            pickup.canPickup = false;
            pickup.enabled = false;
            // pickup.holder stays null -> tool scripts (Flamethrower etc.) remain inert
        }
    }

    // ======================================================
    //  OWNER -> SERVER REQUESTS
    // ======================================================
    public void RequestPrepareTile(Vector3Int cell)
    {
        NetworkMetrics.NoteActionRequested(cell); // telemetry: action latency start
        GameSession.Instance?.RequestPrepareTileServerRpc(cell);
    }

    public void RequestPlantSeed(Vector3Int cell, int itemId)
    {
        NetworkMetrics.NoteActionRequested(cell);
        GameSession.Instance?.RequestPlantSeedServerRpc(cell, itemId, Index);
    }

    public void RequestIrrigate(Vector3Int cell) =>
        GameSession.Instance?.RequestIrrigateServerRpc(cell);

    public void RequestFertilize(Vector3Int cell) =>
        GameSession.Instance?.RequestFertilizeServerRpc(cell);

    public void RequestRemovePlant(Vector3Int cell, bool sabotage) =>
        GameSession.Instance?.RequestRemovePlantServerRpc(cell, Index, sabotage);

    public void RequestPickup(Pickup pickup)
    {
        if (pickup == null) return;
        var no = pickup.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            GameSession.Instance?.RequestPickupServerRpc(no.NetworkObjectId);
    }

    /// <summary>Server granted us a picked-up item (the world object was despawned).</summary>
    public void ReceiveGrantedItem(int itemId)
    {
        if (player.inventory != null && player.inventory.AddItem(itemId))
        {
            player.sfx?.PlayOnPick();
        }
        else
        {
            // Hotbar full: put the item back into the world.
            GameSession.Instance?.RequestSpawnItemServerRpc(itemId, transform.position);
        }
    }

    // ======================================================
    //  EFFECTS TARGETED AT THIS PLAYER (server -> owner)
    // ======================================================
    [ClientRpc]
    public void ApplyPushClientRpc(Vector2 direction, float force)
    {
        if (!IsOwner) return;
        player.ForceIdleState();
        player.ApplyPushForce(direction, force);
    }

    [ClientRpc]
    public void StealWaterClientRpc(float amount)
    {
        if (!IsOwner) return;
        player.waterSupply = Mathf.Max(0f, player.waterSupply - amount);
    }

    // ======================================================
    //  COSMETIC SYNC (irrigate VFX)
    // ======================================================
    public void BroadcastIrrigateVfx(float angle)
    {
        if (IsSpawned && IsOwner)
            IrrigateVfxServerRpc(angle);
    }

    [ServerRpc]
    private void IrrigateVfxServerRpc(float angle) => IrrigateVfxClientRpc(angle);

    [ClientRpc]
    private void IrrigateVfxClientRpc(float angle)
    {
        if (IsOwner) return; // owner already played it locally
        if (player.vfx == null) return;
        player.vfx.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        player.vfx.TriggerVFX("Irrigate");
    }
}
