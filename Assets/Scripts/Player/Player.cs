using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Player : Entity
{
    public PlayerInput input { get; private set; }

    // States
    public Player_IdleState idleState { get; private set; }
    public Player_MoveState moveState { get; private set; }

    [Header("Movement variables")]
    private bool canControl = false; // control flag
    public Vector2 moveInput { get; private set; }
    public float moveSpeed = 8;

    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInput>();
        // sfx = GetComponent<Player_SFX>();

        idleState = new Player_IdleState(this, stateMachine, "idle");
        moveState = new Player_MoveState(this, stateMachine, "move");
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
    }

    public bool IsPlayerMoving() => moveInput.x != 0 || moveInput.y != 0;

    public bool FlipPlayerControlFlag() => canControl = !canControl;

    // Teleport player's transform to given position, useful for start of match or outofbounds
    public void TeleportPlayer(Vector3 position) => transform.position = position;
}
