using UnityEngine;
using System.Collections;

public class Player_SabotageState : Player_ActionState
{
    public Player_SabotageState(Player player, StateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName) { }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("Entering Sabotage State");

        Scissors tool = GetScissorsFromOnHand();
        if (tool != null && !tool.IsOnCooldown)
        {
            player.StartCoroutine(PerformAction(tool));
        }
        else
        {
            Debug.Log("No scissors equipped or still on cooldown");
            stateMachine.ChangeState(player.idleState);
        }
    }

    private Scissors GetScissorsFromOnHand()
    {
        Transform onHand = player.transform.Find("OnHand");
        if (onHand != null && onHand.childCount > 0)
            return onHand.GetChild(0).GetComponent<Scissors>();
        return null;
    }

    private IEnumerator PerformAction(Scissors tool)
    {
        isPerformingAction = true;
        player.FlipPlayerControlFlag();

        Debug.Log("Cortando planta...");

        TileInteraction tileInteraction = player.GetComponentInChildren<TileInteraction>();
        Vector3Int targetCell = tileInteraction != null ? tileInteraction.CurrentCell :
            FarmManager.instance.farmTilemap.WorldToCell(player.transform.position);

        tool.Use(targetCell);

        yield return new WaitForSeconds(tool.CutDuration);

        player.FlipPlayerControlFlag();

        yield return new WaitForSeconds(tool.Cooldown);
        tool.ResetCooldown();

        isPerformingAction = false;

        stateMachine.ChangeState(player.idleState);
    }

    public override void Update()
    {
        base.Update();
    }
}
