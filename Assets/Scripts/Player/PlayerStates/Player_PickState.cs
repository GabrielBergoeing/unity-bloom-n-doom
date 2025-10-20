using UnityEngine;

public class Player_PickState : Player_ActionState
{
    public Player_PickState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        stateMachine.ChangeState(player.idleState);
    }

    public override void Update()
    {
        base.Update();
    }
}
