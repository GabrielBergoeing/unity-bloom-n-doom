public class Player_PrepareGroundState : Player_ActionState
{
    public Player_PrepareGroundState(Player player, StateMachine stateMachine, string animBoolName)
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
            ExecuteAction(player.prepareGroundFrame, player.prepareGroundCooldown, cell => //Define cooldowns in player?
            {
                if (!FarmManager.instance.IsPrepared(cell) || !FarmManager.instance.IsOccupied(cell))
                    FarmManager.instance.PrepareTile(cell); //Change to callable fuction
            })
        );
    }
}
