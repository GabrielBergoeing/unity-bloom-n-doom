using UnityEngine;

public abstract class PlayerState: EntityState
{
    protected Player player;
    protected PlayerInputSet input;

    public PlayerState(Player player, StateMachine stateMachine, string animBoolName) : base(stateMachine, animBoolName)
    {
        this.player = player;

        anim = player.anim;
        rb = player.rb;
    }

    public override void UpdateAnimationParameters()
    {
        base.UpdateAnimationParameters();
        anim.SetFloat("xVelocity", player.moveInput.x);
        anim.SetFloat("yVelocity", player.moveInput.y);

        anim.SetFloat("xFacingDir", player.xFacingDir);
        anim.SetFloat("yFacingDir", player.yFacingDir);
    }

    /*
    private bool CanDash()
    {
        if (!player.hasDash || player.wallDetected || stateMachine.currentState == player.dashState)
            return false;
        return true;
    }
    */
}
