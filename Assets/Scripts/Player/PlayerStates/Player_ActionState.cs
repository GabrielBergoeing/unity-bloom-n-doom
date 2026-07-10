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
        // Explicit set instead of FlipPlayerControlFlag()'s toggle: this state object is a
        // reused singleton, so if ExecuteAction is ever re-entered before a previous run
        // finished, a toggle's parity breaks and canControl can get stuck false forever
        // (player permanently frozen). An explicit Disable/Enable pair can't desync that way.
        player.DisableControl();
        player.SetActionCooldownVisible(true);
        player.SetActionCooldownProgress(0f);

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

        // Animation time + cooldown (sabotage tools, etc), ticked frame-by-frame instead of
        // two WaitForSeconds calls so the visual bar can show live progress while control
        // stays locked (otherwise the player could immediately re-trigger the same action).
        float total = duration + Mathf.Max(0f, cooldown);
        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            player.SetActionCooldownProgress(total > 0f ? elapsed / total : 1f);
            yield return null;
        }

        player.SetActionCooldownVisible(false);
        player.EnableControl();
        isPerformingAction = false;
    }
}
