using UnityEngine;

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
        player.SetVelocity(0, 0);
    }

    public override void Update()
    {
        base.Update();
        //Set player velocity to 0!
    }

    public override void Exit()
    {
        base.Exit();
        isPerformingAction = false;
    }
}
