using UnityEngine;

public class Player_PickState : Player_ActionState
{
    public Player_PickState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        if (player.tile.CanRefillWater())
        {
            sfx.PlayOnRefill();
            player.StartCoroutine(ExecuteAction(player.pickFrame, player.pickCooldown, _ => { player.waterSupply += 10; }));
        }

        else
        {
            Pickup pickup = player.GetPickupNearby();
            if (pickup != null)
            {
                player.StartCoroutine(
                    ExecuteAction(player.pickFrame, player.pickCooldown, _ =>
                    {
                        if (player.inventory.AddItem(pickup.gameObject))
                        {
                            sfx.PlayOnPick();
                            pickup.Pick(player);
                        }
                    }));
            }
            else
                stateMachine.ChangeState(player.idleState);
        }
    }
}
