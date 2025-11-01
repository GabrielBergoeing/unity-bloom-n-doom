public class Player_RemoveState : Player_ActionState
{
    public Player_RemoveState(Player player, StateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName) { }

    public override void Enter()
    {
        base.Enter();

        if (!IsOnHandEmpty())
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        player.StartCoroutine(
            ExecuteAction(player.removeFrame, player.removeCooldown, cell => tile.RemoveInCell())
        );
    }
}

