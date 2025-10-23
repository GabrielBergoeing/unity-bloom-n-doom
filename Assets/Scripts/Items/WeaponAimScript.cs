using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAimScript : MonoBehaviour
{
    private InputAction aimAction;
    private Pickup pickup;
    private PlayerInput playerInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pickup = GetComponent<Pickup>();
    }

    // Update is called once per frame
    void Update()
    {
        if (pickup.isPickedUp == true && playerInput == null)
        {
            playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput != null)
            {
                aimAction = playerInput.actions["Aim"];
            }
        }

        if (pickup.isPickedUp == false)
        {
            playerInput = null;
            aimAction = null;
        }

        if (aimAction == null || playerInput == null) return;

        Vector2 move = aimAction.ReadValue<Vector2>();
        Vector3 dir = new Vector3(move.x, move.y, 0f);
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.up = dir.normalized;
        }
    }
}
