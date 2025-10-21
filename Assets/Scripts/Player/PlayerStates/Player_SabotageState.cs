using UnityEngine;
using System.Collections;

public class Player_SabotageState : Player_ActionState
{
    public Player_SabotageState(Player player, StateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName) { }

    public override void Enter()
    {
        base.Enter();
        Scissors tool = GetItemFromOnHand<Scissors>(); //GetScissorsFromOnHand();

        if (tool != null && !tool.IsOnCooldown)
            player.StartCoroutine(PerformAction(tool));
        else
        {
            Debug.Log("No scissors equipped or still on cooldown");
            stateMachine.ChangeState(player.idleState);
        }
    }
    private IEnumerator PerformAction(Scissors tool)
    {
        player.FlipPlayerControlFlag();

        Debug.Log("Cortando planta...");

        TileInteraction tileInteraction = player.GetComponentInChildren<TileInteraction>();
        Vector3Int targetCell = tileInteraction != null ? tileInteraction.CurrentCell :
            FarmManager.instance.farmTilemap.WorldToCell(player.transform.position);

        tool.Use(targetCell);

        yield return new WaitForSeconds(tool.CutDuration);

        player.FlipPlayerControlFlag();
        isPerformingAction = false;

        yield return new WaitForSeconds(tool.Cooldown);
        tool.ResetCooldown();
    }
}
