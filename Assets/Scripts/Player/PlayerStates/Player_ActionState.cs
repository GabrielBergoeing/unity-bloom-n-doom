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

        if (!isPerformingAction)
            stateMachine.ChangeState(player.idleState);
    }

    public override void Exit()
    {
        base.Exit();
        isPerformingAction = false;
    }

    protected virtual T GetItemFromOnHand<T>() where T : Component //Allows to search for any type of component held on player's hand
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

    protected bool IsOnHandEmpty()
    {
        Transform onHand = player.transform.Find("OnHand");
        return (onHand != null || onHand.childCount > 0);
    }
}
