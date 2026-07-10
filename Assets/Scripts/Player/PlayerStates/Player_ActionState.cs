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
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(0, 0);

        if (!isPerformingAction)
            stateMachine.ChangeState(player.idleState);
    }

    public override void Exit()
    {
        base.Exit();
    }

    // General framework of every action state
    protected IEnumerator ExecuteAction(
        float duration,
        float cooldown,
        System.Action<Vector3Int> applyAction //Stores function with parameters
    ){
        player.FlipPlayerControlFlag();

        // Safe execution just in case
        try 
        {
            // The actual action performed (cut/plant/prepare/etc)
            applyAction(tile.CurrentCell);
        } 
        catch (System.Exception e) 
        {
            Debug.LogError($"[Player_ActionState] Error during action: {e}");
        }

        // Animation time
        yield return new WaitForSeconds(duration);

        // Cooldown (sabotage tools, etc). Control stays locked until this finishes too,
        // otherwise the player could immediately re-trigger the same action again.
        if (cooldown > 0)
        {
            yield return new WaitForSeconds(cooldown);
        }

        player.FlipPlayerControlFlag();
        isPerformingAction = false;
    }
}
