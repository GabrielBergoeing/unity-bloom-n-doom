using UnityEngine;

public class Player_NeutralState : PlayerState
{
    public Player_NeutralState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    public override void Update()
    {
        base.Update();

        //if (player.moveInput.x == player.facingDir && player.wallDetected)
        //return;

        if (player.input.actions["Sabotage"].triggered)
            stateMachine.ChangeState(player.sabotageState);

        if (player.input.actions["Interact"].triggered)
            stateMachine.ChangeState(player.plantState);

        if (player.input.actions["Prepare"].triggered)
            stateMachine.ChangeState(player.prepareGroundState);

        if (player.input.actions["Pickup"].triggered)
            stateMachine.ChangeState(player.pickState);
                
        if (player.input.actions["Irrigate"].triggered)
            stateMachine.ChangeState(player.irrigateState);
    }
}
