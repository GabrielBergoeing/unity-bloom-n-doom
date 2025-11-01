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

    //Allows to search for any type of component held on player's hand
    public virtual T GetItemFromOnHand<T>() where T : Component
    {
        Transform onHand = player.transform.Find("OnHand");

        if (onHand != null && onHand.childCount > 0)
        {
            T item = onHand.GetChild(0).GetComponent<T>();
            if (item != null)
                return item;
        }

        return null;
    }

    //Checks if player is holding something
    public bool IsOnHandEmpty()
    {
        Transform onHand = player.transform.Find("OnHand");
        if (onHand == null) 
            return true;

        foreach (Transform child in onHand)
        {
            // If the child is active and has a visible renderer, hand is NOT empty
            if (child.gameObject.activeInHierarchy)
            {
                var renderer = child.GetComponentInChildren<Renderer>();
                if (renderer != null && renderer.enabled)
                    return false;
            }
        }

        return true; // No visible child found
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
