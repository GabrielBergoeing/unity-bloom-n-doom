using UnityEngine;

public class Player_IrrigateState : Player_ActionState
{
    public Player_IrrigateState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        Debug.Log($"[Player_IrrigateState] Enter on {player.name} (isServer={player.isServer} OwnerIndex={player.OwnerIndex}) waterSupply={player.waterSupply} irrigateCost={player.irrigateCost}");

        if (!player.CanPlayerIrrigate())
        {
            Debug.Log($"[Player_IrrigateState] Not enough water on {player.name}: waterSupply={player.waterSupply} < irrigateCost={player.irrigateCost}");
            stateMachine.ChangeState(player.idleState);
            return;
        }

        // 1 second irrigation time, no cooldown
        player.StartCoroutine(ExecuteAction(player.irrigateFrame, player.irrigateCooldown, cell =>
        {
            sfx.PlayOnIrrigate();
            float before = player.waterSupply;
            player.waterSupply -= player.irrigateCost;
            Debug.Log($"[Player_IrrigateState] {player.name} waterSupply {before} -> {player.waterSupply}");

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
