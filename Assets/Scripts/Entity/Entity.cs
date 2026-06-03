using Mirror;
using System;
//using UnityEditor.PackageManager;
using UnityEngine;

public class Entity : NetworkBehaviour
{
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
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        bool isNetworkSpawnedObject = isServer || isClient;

        if (isNetworkActive)
        {
            if (!isNetworkSpawnedObject)
            {
                // Mirror can be active while some gameplay objects are local-only.
                // Keep local simulation running for those objects.
                stateMachine.UpdateActiveState();
                UpdateClient();
                UpdateLocalPlayer();
                return;
            }

            if (isServer)
                stateMachine.UpdateActiveState();

            if (isClient)
                UpdateClient();

            if (isLocalPlayer)
                UpdateLocalPlayer();
        }
        else
        {
            // Local (non-networked) multiplayer: run all logic without authority checks
            stateMachine.UpdateActiveState();
            UpdateClient();
            UpdateLocalPlayer();
        }
    }

    protected virtual void UpdateClient() { }

    protected virtual void UpdateLocalPlayer() { }

    public void CurrentStateAnimationTrigger()
    {
        stateMachine.currentState.AnimationTrigger();
    }

    //Classical arcade-like movement, has no acceleration or force
    public void SetVelocity(float xVelocity, float yVelocity)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        rb.linearVelocity = new Vector2(xVelocity, yVelocity);
    }
}
