using UnityEngine;

public class Player_MoveState : Player_NeutralState
{
    public Player_MoveState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        sfx.PlayOnMovement();

        // Ramp toward the target speed instead of snapping to it - snapping made starting
        // and stopping feel instant/robotic. rb.linearVelocity itself is the "current"
        // velocity here since Entity.SetVelocity writes straight into it.
        Vector2 target = player.moveInput * player.moveSpeed;
        Vector2 current = player.rb != null ? player.rb.linearVelocity : Vector2.zero;
        float rate = player.IsPlayerMoving() ? player.acceleration : player.deceleration;
        Vector2 smoothed = Vector2.MoveTowards(current, target, rate * Time.deltaTime);

        player.SetVelocity(smoothed.x, smoothed.y);

        // Only settle into Idle once velocity has actually decayed, not the instant input
        // drops to zero - otherwise the deceleration above would only ever get a single
        // frame to run before Idle.Enter() hard-resets velocity to 0 anyway.
        if (!player.IsPlayerMoving() && smoothed.sqrMagnitude < 0.01f)
            stateMachine.ChangeState(player.idleState);
    }
}
