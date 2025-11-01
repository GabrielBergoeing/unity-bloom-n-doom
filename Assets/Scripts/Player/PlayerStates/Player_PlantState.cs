public class Player_PlantState : Player_ActionState
{
    public Player_PlantState(Player player, StateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName) { }

    public override void Enter()
    {
        base.Enter();
        Seed seed = GetItemFromOnHand<Seed>();
        if (seed == null)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        player.StartCoroutine(
            ExecuteAction(seed.PlantDuration, seed.Cooldown, cell => seed.Use(cell))
        );
    }
}

