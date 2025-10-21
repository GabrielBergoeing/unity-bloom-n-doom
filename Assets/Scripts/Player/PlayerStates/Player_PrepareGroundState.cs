using UnityEngine;
using System.Collections;
using UnityEngine.Tilemaps;

public class Player_PrepareGroundState : Player_ActionState
{
    public Player_PrepareGroundState(Player player, StateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName) { }

    public override void Enter()
    {
        base.Enter();

        if (IsOnHandEmpty())
            player.StartCoroutine(PerformAction());
        else
        {
            Debug.Log("Player holding item");
            stateMachine.ChangeState(player.idleState);
        }
    }

    private IEnumerator PerformAction()
    {
        player.FlipPlayerControlFlag();

        Debug.Log("Preparing ground...");

        TileInteraction tileInteraction = player.GetComponentInChildren<TileInteraction>();
        Vector3Int targetCell = tileInteraction != null ? tileInteraction.CurrentCell :
            FarmManager.instance.farmTilemap.WorldToCell(player.transform.position);

        if (!FarmManager.instance.IsPrepared(targetCell) || !FarmManager.instance.IsOccupied(targetCell))
            FarmManager.instance.PrepareTile(targetCell);

        yield return new WaitForSeconds(3);

        player.FlipPlayerControlFlag();
        isPerformingAction = false;
    }
}
