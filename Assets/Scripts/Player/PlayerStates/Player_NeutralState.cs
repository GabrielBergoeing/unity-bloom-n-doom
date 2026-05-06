public class Player_NeutralState : PlayerState
{
    private bool hasIneteraction = false;
    public Player_NeutralState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    public override void Enter()
    {
        base.Enter();
        hasIneteraction = false;
    }
    public override void Update()
    {
        base.Update();

        //if (player.moveInput.x == player.facingDir && player.wallDetected)
        //return;

        if (player.ConsumeSabotageRequest())
        {
            var state = DetermineDisruptionState();

            if (state != null)
                stateMachine.ChangeState(state);
        }

        if (player.ConsumePickupRequest())
            stateMachine.ChangeState(player.pickState);

        if (player.ConsumeInteractRequest() && !hasIneteraction)
        {
            hasIneteraction = true;
            var state = DetermineInteractionState();

            if (state != null)
                stateMachine.ChangeState(state);
        }

        if (player.ConsumeDropRequest())
            player.DropCurrentItem();
    }

    private PlayerState DetermineInteractionState()
    {
        if (IsOnHandEmpty() && tile.CanPrepare())
            return player.prepareGroundState;

        else if (GetItemFromOnHand<Seed>() != null && tile.CanPlant())
            return player.plantState;

        else if (tile.CanIrrigate())
            return player.irrigateState;

        return this;
    }

    private PlayerState DetermineDisruptionState()
    {
        if (IsOnHandEmpty() && tile.CanRemove())
            return player.removeState;

        else if (GetItemFromOnHand<Scissors>() && tile.CanSabotage())
            return player.sabotageState;

        return this;
    }
}
