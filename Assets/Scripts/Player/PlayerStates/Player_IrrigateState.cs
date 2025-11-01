using UnityEngine;

public class Player_IrrigateState : Player_ActionState
{
    public Player_IrrigateState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        if (player.waterSupply < 10)
        {
            Debug.Log("Not enough water");
            stateMachine.ChangeState(player.idleState);
            return;
        }

        // 1 second irrigation time, no cooldown
        player.StartCoroutine(ExecuteAction(1f, 0f, cell =>
        {
            player.waterSupply -= 10;

            // Rotate and play VFX
            if (player.vfx != null)
            {
                Vector3 pos = FarmManager.instance.farmTilemap.GetCellCenterWorld(cell);
                Vector3 dir = pos - player.transform.position;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                player.vfx.transform.rotation = Quaternion.Euler(0, 0, angle);
                player.vfx.TriggerVFX("Irrigate");
            }

            FarmManager.instance.TryIrrigatePlant(cell);
        }));
    }
}
