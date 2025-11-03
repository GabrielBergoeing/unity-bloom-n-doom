using UnityEngine;

public class Entity : MonoBehaviour
{
    private bool kinematicFlag = false;
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    protected StateMachine stateMachine; //-> Helps define the states (actions) entities have

    protected virtual void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();

        stateMachine = new StateMachine();
    }

    protected virtual void Start()
    {

    }

    protected virtual void Update()
    {
        //HandleCollisionDetection(); -> necesarry to add additional collision detectors? to discuss
        stateMachine.UpdateActiveState();
    }

    public void CurrentStateAnimationTrigger()
    {
        stateMachine.currentState.AnimationTrigger();
    }

    //Classical arcade-like movement, has no acceleration or force
    public void SetVelocity(float xVelocity, float yVelocity) => rb.linearVelocity = new Vector2(xVelocity, yVelocity);
}
