using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Player_SabotageState : Player_ActionState
{

    public Player_SabotageState(Player player, StateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName) { }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (!isPerformingAction)
            stateMachine.ChangeState(player.idleState);
    }

    private void OnSabotagePerformed(InputAction.CallbackContext context)
    {
        Scissors tool = null;
        Transform onHand = player.transform.Find("OnHand");
        if (onHand != null && onHand.childCount > 0)
            tool = onHand.GetChild(0).GetComponent<Scissors>();

        if (tool != null && !tool.IsOnCooldown)
            player.StartCoroutine(PerformAction(tool));
    }

    private IEnumerator PerformAction(Scissors tool)
    {
        player.FlipPlayerControlFlag();

        Debug.Log("Cortando la flor...");

        TileInteraction tileInteraction = player.GetComponentInChildren<TileInteraction>();
        Vector3Int targetCell = tileInteraction != null ? tileInteraction.CurrentCell :
            FarmManager.instance.farmTilemap.WorldToCell(player.transform.position);

        tool.Use(targetCell);

        yield return new WaitForSeconds(tool.CutDuration);

        player.FlipPlayerControlFlag();

        yield return new WaitForSeconds(tool.Cooldown);
        tool.ResetCooldown();
    }
}
