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

    // General framework of every action state
    protected IEnumerator ExecuteAction(
        float duration,
        float cooldown,
        System.Action<Vector3Int> applyAction //Stores function with parameters
    ){
        player.FlipPlayerControlFlag();

        // The actual action performed (cut/plant/prepare/etc)
        applyAction(tile.CurrentCell);

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
