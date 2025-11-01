public class Player_SabotageState : Player_ActionState
{
    public Player_SabotageState(Player player, StateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName) { }

    public override void Enter()
    {
        base.Enter();
        Scissors tool = GetItemFromOnHand<Scissors>();
        if (tool == null || tool.IsOnCooldown)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        player.StartCoroutine(
            ExecuteAction(tool.CutDuration, tool.Cooldown, cell => tool.Use(cell))
        );
    }
}
