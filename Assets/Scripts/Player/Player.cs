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
    public NetworkPlayer net { get; private set; }
    #endregion

    #region Network Helpers
    // True when this player instance is controlled on this machine.
    public bool IsLocalOwner => !GameSession.OnlineActive || (net != null && net.IsOwner);

    // Stable match-wide index (0..3). Online it is assigned by the server.
    public int PlayerIndex => GameSession.OnlineActive && net != null
        ? net.Index
        : (input != null ? input.playerIndex : 0);
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
    public int xFacingDir { get; private set; } = 1; // 1 : Right, -1 : Left, 0 : horizontal
    public int yFacingDir { get; private set; } = 1; // 1 : Up, -1 : Down, 0 : vertical

    // Boolean flag that inidicates if player character can be controled
    public bool canControl { get; private set; } = false;

    public List<Pickup> pickupsInRange = new(); // Dynamic lists that stores detected pickups
    #endregion

    #region MonoBehaviour Functions
    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInput>();
        net = GetComponent<NetworkPlayer>();
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
        // Remote copies are driven by the network (transform + animator), not by local logic.
        if (GameSession.OnlineActive && net != null && !net.IsOwner)
            return;

        base.Update();
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
    public void OnEnable() // Enable player control after spawn
    {
        if (GameSession.OnlineActive) return; // NetworkPlayer decides who gets control
        if (canControl) return;
        FlipPlayerControlFlag();
    }

    public void OnDisable() // Disable player control
    {
        if (GameSession.OnlineActive) return;
        if (!canControl) return;
        FlipPlayerControlFlag();
    }

    public void SetControl(bool value) => canControl = value;

    // Applied on remote copies from the owner's synced state (facing drives the hand/hold position).
    public void ApplyRemoteState(Vector2 move, int xFacing, int yFacing)
    {
        moveInput = move;
        xFacingDir = xFacing;
        yFacingDir = yFacing;
    }

    public void OnMovement(InputValue input)
    {
        if (!canControl) 
            moveInput = Vector2.zero;
        moveInput = input.Get<Vector2>();

        DetermineFacingDir();
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
        if (collision.gameObject == this.gameObject) return;
        if (!IsLocalOwner) return; // separation is handled by each player's owner


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
        if (inventory == null || inventory.GetCurrentItemId() < 0) return;

        sfx.PlayOnRemove();

        if (consume)
            inventory.ConsumeCurrent();
        else
            inventory.DropCurrent(); // spawns the world item (server-side when online)
    }
    #endregion

    #region Cheat Functions
    public void SpawnScissors() => CheatSpawn(scissors);

    public void SpawnFlamethrower() => CheatSpawn(flamethrower);

    private void CheatSpawn(GameObject prefab)
    {
        if (prefab == null) return;

        if (GameSession.OnlineActive)
        {
            int itemId = NetworkAssets.Instance != null ? NetworkAssets.Instance.ItemIdOf(prefab) : -1;
            if (itemId >= 0)
                GameSession.Instance?.RequestSpawnItemServerRpc(itemId, transform.position);
        }
        else
        {
            Instantiate(prefab, transform.position, Quaternion.identity);
        }
    }
    #endregion

}