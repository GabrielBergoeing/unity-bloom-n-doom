using UnityEngine;

public class Player_IdleState : PlayerState
{
    public Player_IdleState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.SetVelocity(0, 0);
    }

    public override void Update()
    {
        base.Update();
        Debug.Log("Idling");

        //if (player.moveInput.x == player.facingDir && player.wallDetected)
        //return;

        if (player.IsPlayerMoving())
            stateMachine.ChangeState(player.moveState);
    }
}
