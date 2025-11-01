using UnityEngine;
using System.Collections;

public class Player_ActionState : PlayerState
{
    public bool isPerformingAction = true;

    public Player_ActionState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        isPerformingAction = true;
        player.FlipKinematicFlag();
    }

    public override void Update()
    {
        base.Update();

        if (!isPerformingAction)
            stateMachine.ChangeState(player.idleState);
    }

    public override void Exit()
    {
        base.Exit();
        player.FlipKinematicFlag();
    }

    //Allows to search for any type of component held on player's hand
    protected virtual T GetItemFromOnHand<T>() where T : Component
    {
        Transform onHand = player.transform.Find("OnHand");

        if (onHand != null && onHand.childCount > 0)
        {
            T item = onHand.GetChild(0).GetComponent<T>();
            if (item != null)
                return item;
        }

        return null;
    }

    protected bool IsOnHandEmpty()
    {
        Transform onHand = player.transform.Find("OnHand");
        return onHand == null || onHand.childCount == 0;
    }

    // General framework of every action state
    protected IEnumerator ExecuteAction(
        float duration,
        float cooldown,
        System.Action<Vector3Int> applyAction //Stores function with parameters
    ){
        player.FlipPlayerControlFlag();

        TileInteraction tile = player.GetComponentInChildren<TileInteraction>();
        Vector3Int cell = tile != null
            ? tile.CurrentCell
            : FarmManager.instance.farmTilemap.WorldToCell(player.transform.position);

        // The actual action performed (cut/plant/prepare/etc)
        applyAction(cell);

        // Animation time
        yield return new WaitForSeconds(duration);

        player.FlipPlayerControlFlag();
        isPerformingAction = false;

        // Cooldown (sabotage tools, etc)
        if (cooldown > 0)
        {
            yield return new WaitForSeconds(cooldown);
        }
    }
}
