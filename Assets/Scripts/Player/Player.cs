using UnityEngine;

public class Player : Entity
{
    public PlayerInputSet input { get; private set; }

    // Here we define variables to store states, like
    public Player_IdleState idleState { get; private set; }
    public Player_MoveState moveState { get; private set; }

    [Header("Movement variables")]
    [SerializeField] public Vector2 moveInput { get; private set; }
    public float moveSpeed = 8;

    protected override void Awake()
    {
        base.Awake();
        input = new PlayerInputSet();
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
        Debug.Log(moveInput);
    }

    private void OnEnable()
    {
        input.Enable();

        // Define the Player type control scheme
        input.Player.Movement.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Movement.canceled += ctx => moveInput = Vector2.zero;
    }

    private void OnDisable()
    {
        input.Disable();
    }

    public bool IsPlayerMoving() => moveInput.x != 0 || moveInput.y != 0;

    // Teleport player's transform to given position, useful for start of match or outofbounds
    public void TeleportPlayer(Vector3 position) => transform.position = position;
}
