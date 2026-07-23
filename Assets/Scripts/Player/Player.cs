using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : Entity
{
    #region Components
    public PlayerInput input { get; private set; }
    public Player_VFX vfx { get; private set; }
    public TileInteraction tile { get; private set; }
    public Player_SFX sfx { get; private set; }
    public HotbarSystem inventory { get; private set; }
    private Player_ActionCooldownVisual actionCooldownVisual;
    #endregion

    #region States
    public Player_IdleState idleState { get; private set; }
    public Player_IrrigateState irrigateState { get; private set; }
    public Player_MoveState moveState { get; private set; }
    public Player_PickState pickState { get; private set; }
    public Player_PlantState plantState { get; private set; }
    public Player_PrepareGroundState prepareGroundState { get; private set; }
    public Player_RemoveState removeState { get; private set; }
    public Player_SabotageState sabotageState { get; private set; }
    #endregion

    #region Cheat Variables
    [SerializeField] private GameObject scissors;
    [SerializeField] private GameObject flamethrower;
    #endregion

    #region Interface Variables
    [Header("Movement variables")]
    public float moveSpeed = 8;

    // Player_MoveState ramps rb.linearVelocity toward/away from moveSpeed at these rates
    // (units/sec^2) instead of snapping to it instantly - the instant-velocity version felt
    // stiff/robotic, especially stopping dead the frame a key was released.
    [Range(1f, 200f)] public float acceleration = 70f;
    [Range(1f, 200f)] public float deceleration = 70f;

    public Vector2 moveInput { get; private set; }

    [Header("Irrigate variables")]
    [SyncVar] public float waterSupply = 100;
    [Range(1, 20)] public int irrigateCost = 10;

    [Header("Action Active Frames")]
    // irrigateFrame/irrigateCooldown lowered from 2/2 - watering is done twice per plant
    // (interactionsToMature=2), so the old 4s-per-watering lock made the full plant->water->
    // water cycle take ~11s of pure input-lock time before travel, which is what players were
    // calling out as "planting takes too long".
    [Range(0, 10)] public float irrigateFrame = 1f;
    [Range(0, 10)] public float pickFrame = 1f;
    [Range(0, 10)] public float plantFrame = 1f;
    [Range(0, 10)] public float prepareGroundFrame = 1f;
    [Range(0, 10)] public float removeFrame = 2f;

    [Header("Action Cooldown (in frames)")]
    [Range(0, 10)] public float irrigateCooldown = 1f;
    [Range(0, 10)] public float pickCooldown = 1f;
    [Range(0, 10)] public float plantCooldown = 0f;
    [Range(0, 10)] public float prepareGroundCooldown = 0.5f;
    [Range(0, 10)] public float removeCooldown = 2f;
    #endregion

    #region In-House Variables
    // Handles values to display anim facing dir
    [SyncVar] public int xFacingDir  = 1; // 1 : Right, -1 : Left, 0 : horizontal
    [SyncVar] public int yFacingDir  = 1; // 1 : Up, -1 : Down, 0 : vertical

    // Boolean flag that inidicates if player character can be controled
    [SyncVar] public bool canControl  = false;

    // Stable per-slot scoring index for online matches. Set by OnlineNetworkManager
    // before AddPlayerForConnection so input.playerIndex (unreliable online) is never needed.
    [SyncVar] public int onlinePlayerIndex = -1;

    // Unified scoring key used by CmdRequestPlant, CmdRemovePlant, and Cactus.
    public int OwnerIndex => onlinePlayerIndex >= 0 ? onlinePlayerIndex : (input != null ? input.playerIndex : -1);

    // Client action intents are sent through Commands and consumed by server-side states.
    private bool pickupRequested;
    private bool interactRequested;
    private bool dropRequested;
    private bool sabotageRequested;

    public List<Pickup> pickupsInRange = new(); // Dynamic lists that stores detected pickups

    // "PlayerCam" (a child of this prefab) drives the Screen Space - Camera HUD canvases
    // (UI_Hotbar/UI_WaterSupply) - it's enabled by default on every prefab instance, with no
    // local-vs-remote gating anywhere (unlike its sibling AudioListener, which is disabled by
    // default in the prefab for exactly this reason). Online, every spawned Player - including
    // every OTHER client's copy of every OTHER player - was carrying its own live, full-screen,
    // same-depth camera, so whichever one happened to render last on a given machine silently
    // overwrote everyone else's HUD - e.g. a client watering plants correctly server-side while
    // staring at a remote copy of the host's water bar instead of their own.
    private Camera playerCam;
    #endregion

    #region MonoBehaviour Functions
    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInput>();

        // Online only: keep input disabled until OnStartLocalPlayer() enables it, so remote
        // players' ghost objects don't process local input. Offline, PlayerInput.Instantiate()
        // already pairs the requested device/control scheme synchronously inside its own
        // OnEnable() - disabling here would run BEFORE that OnEnable ever fires (Awake always
        // runs before OnEnable on a freshly instantiated object), and re-enabling later in
        // Start() would re-trigger OnEnable() after Unity already cleared Instantiate()'s
        // requested index/scheme/device, so it silently falls back to generic auto-pairing
        // (this was why player 1 always ended up on keyboard regardless of the device used).
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (isNetworkActive)
            input.enabled = false;

        // Same reasoning as input above: default this off online, then only OnStartLocalPlayer
        // (or the offline fallback in Start()) turns it back on for whoever this machine's
        // actual player is.
        playerCam = GetComponentInChildren<Camera>(true);
        if (isNetworkActive)
            SetPlayerCamActive(false);

        Debug.Log($"[Player.Awake] {name} isNetworkActive={isNetworkActive} input.enabled={input.enabled} playerIndex={input.playerIndex} scheme={input.currentControlScheme} devices=[{string.Join(", ", input.devices)}]");

        vfx = GetComponentInChildren<Player_VFX>();
        tile = GetComponentInChildren<TileInteraction>();
        sfx = GetComponent<Player_SFX>();
        inventory = GetComponent<HotbarSystem>();

        // Built at runtime so none of the 5 character prefabs need manual editing.
        actionCooldownVisual = GetComponent<Player_ActionCooldownVisual>();
        if (actionCooldownVisual == null)
            actionCooldownVisual = gameObject.AddComponent<Player_ActionCooldownVisual>();

        idleState = new Player_IdleState(this, stateMachine, "idle");
        irrigateState = new Player_IrrigateState(this, stateMachine, "irrigate");
        moveState = new Player_MoveState(this, stateMachine, "move");
        pickState = new Player_PickState(this, stateMachine, "pick");
        plantState = new Player_PlantState(this, stateMachine, "plant");
        prepareGroundState = new Player_PrepareGroundState(this, stateMachine, "plant");
        removeState = new Player_RemoveState(this, stateMachine, "remove");
        sabotageState = new Player_SabotageState(this, stateMachine, "sabotage");
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState);

        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        bool isNetworkSpawnedObject = isServer || isClient;

        Debug.Log($"[Player.Start] {name} isNetworkActive={isNetworkActive} input.enabled(before)={input.enabled} playerIndex={input.playerIndex} scheme={input.currentControlScheme} devices=[{string.Join(", ", input.devices)}]");

        // In local mode or local-only objects under an active Mirror session,
        // Mirror ownership callbacks do not grant local authority.
        if (!isNetworkActive || !isNetworkSpawnedObject)
        {
            input.enabled = true;
            canControl = true;
            SetPlayerCamActive(true);
        }

        Debug.Log($"[Player.Start] {name} input.enabled(after)={input.enabled} playerIndex={input.playerIndex} scheme={input.currentControlScheme} devices=[{string.Join(", ", input.devices)}]");
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void UpdateLocalPlayer()
    {
        if (input == null || !input.enabled) return;

        bool useNetworkCommands = isServer || isClient;

        if (input.actions["Pickup"].triggered)
        {
            if (useNetworkCommands) CmdRequestPickup();
            else pickupRequested = true;
        }

        if (input.actions["Interact"].triggered)
        {
            if (useNetworkCommands) CmdRequestInteract();
            else interactRequested = true;
        }

        if (input.actions["Drop"].triggered)
        {
            if (useNetworkCommands) CmdRequestDrop();
            else dropRequested = true;
        }

        if (input.actions["Sabotage"].triggered)
        {
            if (useNetworkCommands) CmdRequestSabotage();
            else sabotageRequested = true;
        }

#if UNITY_EDITOR
        // Cheats only compiled into Editor builds now - they were reachable by any player
        // in real builds (free water refill, free tools) via plain keybinds.
        if (input.actions["CheatRefill"].triggered)
        {
            if (useNetworkCommands) CmdCheatRefill();
            else waterSupply = 100;
        }

        if (input.actions["CheatScissors"].triggered)
            CmdCheatSpawnScissors();

        if (input.actions["CheatFlamethrower"].triggered)
            CmdCheatSpawnFlamethrower();
#endif
    }
    #endregion

    // Some gamepads don't spring back to exactly (0,0) when the stick is released - they
    // settle on a tiny residual value that can land in the opposite quadrant from whatever
    // direction was actually held, which momentarily flips the facing to face backwards
    // right as the player lets go. A plain "== Vector2.zero" check only catches a perfect
    // zero, so it doesn't catch this; anything below this magnitude is treated as no input
    // instead, same as an exact zero.
    private const float facingDeadzone = 0.2f;

    private void DetermineFacingDir()
    {
        if (moveInput.sqrMagnitude < facingDeadzone * facingDeadzone)
            return; // No change if input is at/near zero (covers noisy stick release)

        float absX = Mathf.Abs(moveInput.x);
        float absY = Mathf.Abs(moveInput.y);

        // Hysteresis: once an axis is dominant, require the other axis to clearly overtake
        // it (not just barely edge ahead) before switching. Without this, holding a gamepad
        // stick near a diagonal made the sprite flicker between two facings every frame from
        // ordinary stick noise, since a plain ">" comparison flips on the tiniest fluctuation.
        const float hysteresis = 0.15f;
        bool xCurrentlyDominant = xFacingDir != 0;
        bool xDominant = xCurrentlyDominant ? (absX >= absY - hysteresis) : (absX > absY + hysteresis);

        if (xDominant)
        {
            // Horizontal movement dominates
            yFacingDir = 0;
            xFacingDir = moveInput.x > 0 ? 1 : -1;
        }
        else
        {
            // Vertical movement dominates
            xFacingDir = 0;
            yFacingDir = moveInput.y > 0 ? 1 : -1;
        }
    }

    #region Public Functions
    //public void OnEnable() // Enable player control after spawn
    //{
    //    if (canControl) return;
    //    FlipPlayerControlFlag();
    //}

    //public void OnDisable() // Disable player control
    //{
    //    if (!canControl) return;
    //    FlipPlayerControlFlag();
    //}

    public void EnableControl()
    {
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (!isNetworkActive || isServer)
            canControl = true;
    }

    public void DisableControl()
    {
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (!isNetworkActive || isServer)
        {
            canControl = false;

            // A player already holding a movement key won't fire another OnMovement
            // event to naturally re-zero moveInput once canControl flips false, so
            // whoever was mid-move when this is called would otherwise keep sliding
            // (Player_MoveState.Update reapplies velocity from moveInput every frame).
            moveInput = Vector2.zero;
            SetVelocity(0, 0);
        }
    }

    public void SetActionCooldownVisible(bool visible) => actionCooldownVisual?.SetVisible(visible);
    public void SetActionCooldownProgress(float t) => actionCooldownVisual?.SetProgress(t);

    public void OnMovement(InputValue inputValue)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isLocalPlayer) return;

        Vector2 val = inputValue.Get<Vector2>();

        if (!canControl)
            val = Vector2.zero;

        if (isNetworkSpawnedObject)
            CmdSendMovement(val);
        else
        {
            moveInput = val;
            DetermineFacingDir();
        }
    }

    [Command]
    void CmdSendMovement(Vector2 input)
    {
        if (!canControl)
            input = Vector2.zero;

        moveInput = input;
        DetermineFacingDir();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        input.enabled = true;
        SetPlayerCamActive(true);

        // OnlineNetworkManager.OnServerAddPlayer spawns this object with no device/scheme
        // info at all (the server has no way to know a remote client's local hardware), so
        // re-enabling PlayerInput above falls back to Unity's generic "find a scheme
        // matching any currently-unpaired device" detection - a keyboard is essentially
        // always unpaired, so this reliably resolves to the Keyboard scheme even for a
        // gamepad player. Explicitly (re)pairing to whichever device this client actually
        // used most recently (the same signal UI_MatchMenuOnline's own Gamepad.current/
        // Keyboard.current polling already relies on for lobby navigation) fixes it.
        if (Gamepad.current != null)
        {
            input.SwitchCurrentControlScheme("Controller", Gamepad.current);
        }
        else if (Keyboard.current != null)
        {
            if (Mouse.current != null)
                input.SwitchCurrentControlScheme("Keyboard", Keyboard.current, Mouse.current);
            else
                input.SwitchCurrentControlScheme("Keyboard", Keyboard.current);
        }

        CmdSetControl(true);
    }

    private void SetPlayerCamActive(bool active)
    {
        if (playerCam != null)
            playerCam.gameObject.SetActive(active);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        OnlineMatchManager.instance?.RegisterPlayer(this);
    }

    public override void OnStopServer()
    {
        OnlineMatchManager.instance?.UnregisterPlayer(this);
        base.OnStopServer();
    }

    [Command]
    void CmdSetControl(bool value)
    {
        canControl = value;
    }

    [Command]
    private void CmdRequestPickup()
    {
        if (!canControl) return;
        pickupRequested = true;
    }

    [Command]
    private void CmdRequestInteract()
    {
        if (!canControl) return;
        interactRequested = true;
    }

    [Command]
    private void CmdRequestDrop()
    {
        if (!canControl) return;
        dropRequested = true;
    }

    [Command]
    private void CmdRequestSabotage()
    {
        if (!canControl) return;
        sabotageRequested = true;
    }

    // NOW PUBLIC: command to request shooting from the currently equipped item (Flamethrower)
    [Command]
    public void CmdRequestShoot()
    {
        int connId = connectionToClient != null ? connectionToClient.connectionId : -1;

        if (!canControl)
        {
            Debug.LogWarning($"[Player][CmdRequestShoot] Rejected: canControl=false connection={connId}");
            return;
        }

        if (inventory == null)
        {
            Debug.LogWarning($"[Player][CmdRequestShoot] Rejected: no HotbarSystem on {name} connection={connId}");
            return;
        }

        GameObject currentItem = inventory.GetCurrentItem();
        if (currentItem == null)
        {
            Debug.LogWarning($"[Player][CmdRequestShoot] Rejected: current slot ({inventory.GetCurrentSlot()}) is empty on server connection={connId}");
            return;
        }

        Flamethrower ft = currentItem.GetComponent<Flamethrower>();
        if (ft == null)
        {
            Debug.LogWarning($"[Player][CmdRequestShoot] Rejected: current item '{currentItem.name}' has no Flamethrower component connection={connId}");
            return;
        }

        Vector2 ownerVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        ft.ServerShoot(ownerVelocity);
    }

    [Command]
    private void CmdCheatRefill()
    {
        waterSupply = 100;
    }

    [Command]
    private void CmdCheatSpawnScissors()
    {
        SpawnScissors();
    }

    [Command]
    private void CmdCheatSpawnFlamethrower()
    {
        SpawnFlamethrower();
    }

    [Command]
    public void CmdIrrigateCell(int x, int y, int z)
    {
        if (!isServer || FarmManager.instance == null || !canControl) return;
        Vector3Int cell = new Vector3Int(x, y, z);
        FarmManager.instance.TryIrrigatePlant(cell);
    }

    [Command]
    public void CmdFertilizeCell(int x, int y, int z)
    {
        if (!isServer || FarmManager.instance == null || !canControl) return;
        Vector3Int cell = new Vector3Int(x, y, z);
        FarmManager.instance.TryFertilizePlant(cell);
    }

    [Command]
    public void CmdRemovePlant(int x, int y, int z)
    {
        if (!isServer || FarmManager.instance == null || !canControl) return;
        Vector3Int cell = new Vector3Int(x, y, z);
        int requesterIndex = OwnerIndex;
        FarmManager.instance.TryRemovePlant(cell, requesterIndex);
    }

    // Command para que el servidor plante (recibe nombre de prefab y lo carga en servidor)
    [Command]
    public void CmdRequestPlant(int x, int y, int z, string plantPrefabName)
    {
        Debug.Log($"[Player][CmdRequestPlant] Server received request prefab={plantPrefabName} connection={(connectionToClient!=null?connectionToClient.connectionId:-1)}");
        if (!isServer || FarmManager.instance == null || !canControl) return;

        GameObject prefab = Resources.Load<GameObject>(plantPrefabName);
        if (prefab == null)
        {
            Debug.LogError($"[Player][CmdRequestPlant] Prefab not found in Resources: {plantPrefabName}. Reg�stralo en NetworkManager.SpawnablePrefabs o ponlo en Resources/");
            return;
        }

        Vector3Int cell = new Vector3Int(x, y, z);
        int playerIndex = OwnerIndex;
        FarmManager.instance.PlantSeed(cell, playerIndex, prefab);
    }

    // Command para que el servidor prepare el tile
    [Command]
    public void CmdPrepareTile(int x, int y, int z)
    {
        Debug.Log($"[Player][CmdPrepareTile] Server received prepare request from connection={(connectionToClient!=null?connectionToClient.connectionId:-1)} cell=({x},{y},{z})");
        if (!isServer || FarmManager.instance == null || !canControl)
        {
            Debug.LogWarning("[Player][CmdPrepareTile] Rejected: no server, no FarmManager or can't control.");
            return;
        }

        Vector3Int cell = new Vector3Int(x, y, z);
        FarmManager.instance.PrepareTile(cell);
    }

    public bool ConsumePickupRequest()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return false;

        if (!pickupRequested) return false;
        pickupRequested = false;
        return true;
    }

    public bool ConsumeInteractRequest()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return false;

        if (!interactRequested) return false;
        interactRequested = false;
        return true;
    }

    public bool ConsumeDropRequest()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return false;

        if (!dropRequested) return false;
        dropRequested = false;
        return true;
    }

    public bool ConsumeSabotageRequest()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return false;

        if (!sabotageRequested) return false;
        sabotageRequested = false;
        return true;
    }

    public bool IsPlayerMoving() => moveInput.x != 0 || moveInput.y != 0;

    public bool CanPlayerIrrigate() => waterSupply >= irrigateCost;

    public bool FlipPlayerControlFlag() => canControl = !canControl;

    // Teleport player's transform to given position, useful for start of match or outofbounds
    public void TeleportPlayer(Vector3 position) => transform.position = position;
    public Pickup GetPickupNearby() => pickupsInRange.Count > 0 ? pickupsInRange[0] : null;
    #endregion

    #region Physics Functions
    public void ForceIdleState() // Interrupt current action and force idle state
    {
        // Every other state-mutating method on this class (ApplyPushForce, SetVelocity,
        // DisableControl...) has this guard - this one didn't, so every client whose local
        // physics detected a Watergun trigger overlap (not just the server) was locally
        // yanking the player's own state machine back to idle, fighting the server's
        // authoritative state on every remote client.
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        if (stateMachine != null && idleState != null)
        {
            stateMachine.ChangeState(idleState);
        }
    }

    public void ApplyPushForce(Vector2 direction, float force)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;

            // Used to also call rb.AddForce(..., ForceMode2D.Impulse) here, but the very next
            // line immediately overwrote rb.linearVelocity via SetVelocity before physics ever
            // integrated that impulse - it was dead code, the real push was always just this.
            SetVelocity(direction.x * force * 0.3f, direction.y * force * 0.3f);

            StartCoroutine(ValidatePushDestinationCo());
        }
    }

    // Knockback (e.g. Watergun) had no bounds/forbidden-tile check at all - a player could be
    // shoved into water/wall/concrete tiles. A physics-based push makes the exact landing cell
    // hard to predict up front, so this corrects after the fact instead: track the last cell
    // that was still legal while the push plays out, then snap back to it if the player ends
    // up somewhere illegal once it settles.
    private IEnumerator ValidatePushDestinationCo()
    {
        const float settleTime = 0.35f;
        Vector3 safePosition = transform.position;
        float elapsed = 0f;

        while (elapsed < settleTime)
        {
            elapsed += Time.deltaTime;

            if (!IsCurrentCellForbidden())
                safePosition = transform.position;

            yield return null;
        }

        if (IsCurrentCellForbidden())
        {
            transform.position = safePosition;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }

    private bool IsCurrentCellForbidden()
    {
        if (FarmManager.instance == null || FarmManager.instance.farmTilemap == null)
            return false;

        Vector3Int cell = FarmManager.instance.farmTilemap.WorldToCell(transform.position);
        return FarmManager.instance.IsWaterTile(cell)
            || FarmManager.instance.IsWallTile(cell)
            || FarmManager.instance.IsConcreteTile(cell);
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        if (collision.gameObject == this.gameObject) return;
        
        Player otherPlayer = collision.gameObject.GetComponent<Player>();
        if (otherPlayer != null)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                
                rb.bodyType = RigidbodyType2D.Kinematic;
                
                Vector2 separationDirection = (transform.position - otherPlayer.transform.position).normalized;
                transform.position += (Vector3)(separationDirection * 0.1f);
                
                StartCoroutine(RestoreRigidbodyType());
            }
        }
    }

    private System.Collections.IEnumerator RestoreRigidbodyType()
    {
        yield return new WaitForFixedUpdate();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
    }
    #endregion

    #region First To Be Refactor
    public void DropCurrentItem(bool consume = false, bool thrown = false)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        sfx.PlayOnRemove();
        var item = inventory.GetCurrentItem();
        if (item == null) return;

        // Inventory handles slot removal & reparenting
        inventory.RemoveItem(item, consume);

        // Re-enable pickup collider & drop in world
        var pickup = item.GetComponent<Pickup>();
        pickup?.Drop(this);

        item.transform.parent = null;
        item.transform.position = transform.position;
    }
    #endregion

    #region Cheat Functions
    public void SpawnScissors()
    {
        if (scissors != null)
        {
            Instantiate(scissors, transform.position, Quaternion.identity);
        }
    }

    public void SpawnFlamethrower()
    {
        if (flamethrower != null)
        {
            Instantiate(flamethrower, transform.position, Quaternion.identity);
        }
    }
    #endregion

}