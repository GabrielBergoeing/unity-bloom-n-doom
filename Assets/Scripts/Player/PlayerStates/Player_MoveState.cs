public class Player_MoveState : Player_NeutralState
{
    public Player_MoveState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        player.sfx.PlayOnMovement();

        if (!player.IsPlayerMoving()) //|| player.wallDetected)
            stateMachine.ChangeState(player.idleState);

        player.SetVelocity(player.moveInput.x * player.moveSpeed, player.moveInput.y * player.moveSpeed);
    }
}
