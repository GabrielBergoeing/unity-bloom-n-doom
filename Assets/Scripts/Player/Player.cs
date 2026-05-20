using Mirror;
using System;
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
    public Vector2 moveInput { get; private set; }

    [Header("Irrigate variables")]
    public float waterSupply = 100;
    [Range(1, 20)] public int irrigateCost = 10;

    [Header("Action Active Frames")]
    [Range(0, 10)] public float irrigateFrame = 2f;
    [Range(0, 10)] public float pickFrame = 1f;
    [Range(0, 10)] public float plantFrame = 1f;
    [Range(0, 10)] public float prepareGroundFrame = 1f;
    [Range(0, 10)] public float removeFrame = 2f;

    [Header("Action Cooldown (in frames)")]
    [Range(0, 10)] public float irrigateCooldown = 2f;
    [Range(0, 10)] public float pickCooldown = 1f;
    [Range(0, 10)] public float plantCooldown = 0f;
    [Range(0, 10)] public float prepareGroundCooldown = 1f;
    [Range(0, 10)] public float removeCooldown = 2f;
    #endregion

    #region In-House Variables
    // Handles values to display anim facing dir
    [SyncVar] public int xFacingDir  = 1; // 1 : Right, -1 : Left, 0 : horizontal
    [SyncVar] public int yFacingDir  = 1; // 1 : Up, -1 : Down, 0 : vertical

    // Boolean flag that inidicates if player character can be controled
    [SyncVar] public bool canControl  = false;

    // Client action intents are sent through Commands and consumed by server-side states.
    private bool pickupRequested;
    private bool interactRequested;
    private bool dropRequested;
    private bool sabotageRequested;

    public List<Pickup> pickupsInRange = new(); // Dynamic lists that stores detected pickups
    #endregion

    #region MonoBehaviour Functions
    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInput>();
        input.enabled = false;

        vfx = GetComponentInChildren<Player_VFX>();
        tile = GetComponentInChildren<TileInteraction>();
        sfx = GetComponent<Player_SFX>();
        inventory = GetComponent<HotbarSystem>();

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
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void UpdateLocalPlayer()
    {
        if (input == null || !input.enabled) return;

        if (input.actions["Pickup"].triggered)
            CmdRequestPickup();

        if (input.actions["Interact"].triggered)
            CmdRequestInteract();

        if (input.actions["Drop"].triggered)
            CmdRequestDrop();

        if (input.actions["Sabotage"].triggered)
            CmdRequestSabotage();

        if (input.actions["CheatRefill"].triggered)
            CmdCheatRefill();

        if (input.actions["CheatScissors"].triggered)
            CmdCheatSpawnScissors();

        if (input.actions["CheatFlamethrower"].triggered)
            CmdCheatSpawnFlamethrower();
    }
    #endregion

    private void DetermineFacingDir()
    {
        if (moveInput == Vector2.zero)
            return; // No change if no input

        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
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
        if (isServer)
            canControl = true;
    }

    public void DisableControl()
    {
        if (isServer)
            canControl = false;
    }

    public void OnMovement(InputValue inputValue)
    {
        if (!isLocalPlayer) return;

        Vector2 input = inputValue.Get<Vector2>();

        if (!canControl)
            input = Vector2.zero;

        CmdSendMovement(input);
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
        CmdSetControl(true);
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
        if (!canControl) return;

        if (inventory == null) return;

        GameObject currentItem = inventory.GetCurrentItem();
        if (currentItem == null) return;

        Flamethrower ft = currentItem.GetComponent<Flamethrower>();
        if (ft != null)
        {
            Vector2 ownerVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
            ft.ServerShoot(ownerVelocity);
        }
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
        if (!isServer || FarmManager.instance == null) return;
        Vector3Int cell = new Vector3Int(x, y, z);
        FarmManager.instance.TryIrrigatePlant(cell);
    }

    [Command]
    public void CmdFertilizeCell(int x, int y, int z)
    {
        if (!isServer || FarmManager.instance == null) return;
        Vector3Int cell = new Vector3Int(x, y, z);
        FarmManager.instance.TryFertilizePlant(cell);
    }

    [Command]
    public void CmdRemovePlant(int x, int y, int z)
    {
        if (!isServer || FarmManager.instance == null) return;
        Vector3Int cell = new Vector3Int(x, y, z);
        // usa el input.playerIndex del cliente que llamó el Command como comprobación
        int requesterIndex = input != null ? input.playerIndex : -1;
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
            Debug.LogError($"[Player][CmdRequestPlant] Prefab not found in Resources: {plantPrefabName}. Regístralo en NetworkManager.SpawnablePrefabs o ponlo en Resources/");
            return;
        }

        Vector3Int cell = new Vector3Int(x, y, z);
        int playerIndex = input != null ? input.playerIndex : (connectionToClient != null ? connectionToClient.connectionId : -1);
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

    [Server]
    public bool ConsumePickupRequest()
    {
        if (!pickupRequested) return false;
        pickupRequested = false;
        return true;
    }

    [Server]
    public bool ConsumeInteractRequest()
    {
        if (!interactRequested) return false;
        interactRequested = false;
        return true;
    }

    [Server]
    public bool ConsumeDropRequest()
    {
        if (!dropRequested) return false;
        dropRequested = false;
        return true;
    }

    [Server]
    public bool ConsumeSabotageRequest()
    {
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
        if (stateMachine != null && idleState != null)
        {
            stateMachine.ChangeState(idleState);
        }
    }

    public void ApplyPushForce(Vector2 direction, float force)
    {
        if (!isServer) return;

        if (rb != null)
        {
            RigidbodyType2D originalType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Dynamic;

            rb.linearVelocity = Vector2.zero;
            rb.AddForce(direction.normalized * force, ForceMode2D.Impulse);

            SetVelocity(direction.x * force * 0.3f, direction.y * force * 0.3f);


        }
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isServer) return;

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
        if (!isServer) return;

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