using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Player : Entity
{
    // Components
    public PlayerInput input { get; private set; }

    // States
    public Player_IdleState idleState { get; private set; }
    public Player_IrrigateState irrigateState { get; private set; }
    public Player_MoveState moveState { get; private set; }
    public Player_PickState pickState { get; private set; }
    public Player_PlantState plantState { get; private set; }
    public Player_SabotageState sabotageState { get; private set; }

    [Header("Movement variables")]
    private bool canControl = false; // control flag
    public Vector2 moveInput { get; private set; }
    public float moveSpeed = 8;

    // Handles values to display anim facing dir
    public int xFacingDir { get; private set; } = 1; // 1 : Right, -1 : Left, 0 : horizontal
    public int yFacingDir { get; private set; } = 1; // 1 : Up, -1 : Down, 0 : vertical

    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInput>();
        // sfx = GetComponent<Player_SFX>();

        idleState = new Player_IdleState(this, stateMachine, "idle");
        irrigateState = new Player_IrrigateState(this, stateMachine, "irrigate");
        moveState = new Player_MoveState(this, stateMachine, "move");
        pickState = new Player_PickState(this, stateMachine, "pick");
        plantState = new Player_PlantState(this, stateMachine, "plant");
        sabotageState = new Player_SabotageState(this, stateMachine, "sabotage");
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState);

        Camera playerCam = GetComponentInChildren<Camera>();
        if (playerCam != null)
        {
            TileInteraction tileInteraction = FindFirstObjectByType<TileInteraction>();

            if (tileInteraction != null)
            {
                tileInteraction.SetCamera(playerCam);
            }
            else
            {
                Debug.LogWarning("No se encontr� TileInteraction en la escena.");
            }
        }
        else
        {
            Debug.LogWarning("El jugador no tiene una c�mara hija asignada.");
        }
    }

    protected override void Update()
    {
        base.Update();
    }

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

    public void OnEnable() // Enable player control after spawn
    {
        if (canControl) return;
        FlipPlayerControlFlag();
    }

    public void OnDisable() // Disable player control
    {
        if (!canControl) return;
        FlipPlayerControlFlag();
    }

    public void OnMovement(InputValue input)
    {
        if (!canControl) return;
        moveInput = input.Get<Vector2>();

        DetermineFacingDir();
    }

    public bool IsPlayerMoving() => moveInput.x != 0 || moveInput.y != 0;

    public bool FlipPlayerControlFlag() => canControl = !canControl;

    // Teleport player's transform to given position, useful for start of match or outofbounds
    public void TeleportPlayer(Vector3 position) => transform.position = position;
}
