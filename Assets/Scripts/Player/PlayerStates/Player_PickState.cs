using UnityEngine;

public class Player_PickState : Player_ActionState
{
    public Player_PickState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        // Evitar NullReference si tile o sfx no están asignados
        if (player.tile != null && player.tile.CanRefillWater())
        {
            if (sfx != null) sfx.PlayOnRefill();
            player.StartCoroutine(ExecuteAction(player.pickFrame, player.pickCooldown, _ => { player.waterSupply += 10; }));
            return;
        }

        Pickup pickup = player.GetPickupNearby();
        if (pickup != null)
        {
            player.StartCoroutine(
                ExecuteAction(player.pickFrame, player.pickCooldown, _ =>
                {
                    // Cliente propietario pide al servidor que recoja el item
                    if (player.isLocalPlayer)
                    {
                        player.CmdPickItem(pickup);
                        if (sfx != null) sfx.PlayOnPick();
                    }
                }));
        }
        else
        {
            stateMachine.ChangeState(player.idleState);
        }
    }
}
