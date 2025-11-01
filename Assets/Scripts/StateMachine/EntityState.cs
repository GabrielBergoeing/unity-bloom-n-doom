using UnityEngine;

public abstract class EntityState
{
    protected StateMachine stateMachine;
    protected string animBoolName;
    protected Animator anim;
    protected Rigidbody2D rb;

    protected float stateTimer; //-> Timer auxiliary for specific states like stunlock or attack delay
    protected bool triggerCalled; //-> Boolean to signal animations

    public EntityState(StateMachine stateMachine, string animBoolName)
    {
        this.stateMachine = stateMachine;
        this.animBoolName = animBoolName;
    }

    public virtual void Enter()
    {
        anim.SetBool(animBoolName, true);
        triggerCalled = false;
        Debug.Log("Enters " + animBoolName + " state");
    }

    public virtual void Update()
    {
        stateTimer -= Time.deltaTime;
        UpdateAnimationParameters();
    }

    public virtual void Exit()
    {
        anim.SetBool(animBoolName, false);
    }

    public void AnimationTrigger()
    {
        triggerCalled = true;
    }

    public virtual void UpdateAnimationParameters()
    {
        
    }
}
