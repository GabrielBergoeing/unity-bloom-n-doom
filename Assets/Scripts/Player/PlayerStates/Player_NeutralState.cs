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

        if (input.actions["Sabotage"].triggered)
        {
            var state = DetermineDisruptionState();

            if (state != null)
                stateMachine.ChangeState(state);
        }

        if (input.actions["Pickup"].triggered)
            stateMachine.ChangeState(player.pickState);

        if (input.actions["Interact"].triggered && !hasIneteraction)
        {
            hasIneteraction = true;
            var state = DetermineInteractionState();

            if (state != null)
                stateMachine.ChangeState(state);
        }

        if (input.actions["Drop"].triggered)
            player.DropCurrentItem();
        
        if (input.actions["CheatRefill"].triggered)
            player.waterSupply = 100;
        
        if (input.actions["CheatScissors"].triggered)
            player.SpawnScissors();
        
        if (input.actions["CheatFlamethrower"].triggered)
            player.SpawnFlamethrower();
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
