using UnityEngine;

public class Player_IrrigateState : Player_ActionState
{
    public Player_IrrigateState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        if (!player.CanPlayerIrrigate())
        {
            Debug.Log("Not enough water");
            stateMachine.ChangeState(player.idleState);
            return;
        }

        // 1 second irrigation time, no cooldown
        player.StartCoroutine(ExecuteAction(player.irrigateFrame, player.irrigateCooldown, cell =>
        {
            sfx.PlayOnIrrigate();
            player.waterSupply -= player.irrigateCost;

            // Rotate and play VFX
            if (player.vfx != null)
            {
                Vector3 dir = cell - player.transform.position;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                player.vfx.transform.rotation = Quaternion.Euler(0, 0, angle);
                player.vfx.TriggerVFX("Irrigate");
            }

            tile.IrrigateInCell();
        }));
    }
}
