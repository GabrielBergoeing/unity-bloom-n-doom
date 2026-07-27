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
        player.SetControl(false);

        // The actual action performed (cut/plant/prepare/etc)
        try
        {
            applyAction(tile.CurrentCell);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }

        float total = duration + Mathf.Max(0f, cooldown);
        float elapsed = 0f;

        player.cooldownVisual?.SetVisible(true);
        player.cooldownVisual?.SetProgress(0f);

        // Animation time
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            player.cooldownVisual?.SetProgress(total > 0 ? elapsed / total : 1f);
            yield return null;
        }

        player.SetControl(true);
        isPerformingAction = false;

        // Cooldown (sabotage tools, etc)
        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            player.cooldownVisual?.SetProgress(elapsed / total);
            yield return null;
        }

        player.cooldownVisual?.SetVisible(false);
    }
}
