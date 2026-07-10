using UnityEngine;

public class Player_PickState : Player_ActionState
{
    public Player_PickState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        // Picking up an item shouldn't freeze the player like planting/watering does - apply
        // it instantly and return to idle the same frame instead of running it through
        // ExecuteAction, which locks movement/control for pickFrame + pickCooldown seconds.
        if (player.tile.CanRefillWater())
        {
            sfx.PlayOnRefill();
            player.waterSupply += 10;
        }
        else
        {
            Pickup pickup = player.GetPickupNearby();
            if (pickup != null && player.inventory.AddItem(pickup.gameObject))
            {
                sfx.PlayOnPick();
                pickup.Pick(player);
            }
        }

        stateMachine.ChangeState(player.idleState);
    }
}
