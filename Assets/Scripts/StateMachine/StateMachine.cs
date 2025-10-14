using UnityEngine;

public class StateMachine 
{
    public EntityState currentState { get; private set; }


    public virtual void Initialize(EntityState startState)
    {
        currentState = startState;
        currentState.Enter();
    }

    public virtual void ChangeState(EntityState newState)
    {
        currentState.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public virtual void UpdateActiveState()
    {
        currentState.Update();        
    }
}

