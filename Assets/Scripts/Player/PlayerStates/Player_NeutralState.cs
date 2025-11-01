using UnityEngine;

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

        if (player.input.actions["Sabotage"].triggered)
            stateMachine.ChangeState(player.sabotageState);

        if (player.input.actions["Pickup"].triggered)
            stateMachine.ChangeState(player.pickState);

        if (player.input.actions["Interact"].triggered)
        {
            var state = Resolve();

            if (state != null)
                stateMachine.ChangeState(state);
        }
    }

    private PlayerState Resolve()
    {
        Vector3Int cell = player.tile.CurrentCell;

        if (IsOnHandEmpty() &&
            !FarmManager.instance.IsPrepared(cell) &&
            !FarmManager.instance.IsOccupied(cell))
        {
            return player.prepareGroundState;
        }

        if (GetItemFromOnHand<Seed>() != null &&
            FarmManager.instance.IsPrepared(cell) &&
            !FarmManager.instance.IsOccupied(cell))
        {
            return player.plantState;
        }

        if (FarmManager.instance.IsOccupied(cell))
        {
            return player.irrigateState;
        }

        return null;
    }
}
