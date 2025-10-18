using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponRotation : MonoBehaviour
{
	Camera mainCamera;
	Player player;
	PlayerInput playerInput;
	InputAction moveAction;


	void Start()
	{
		mainCamera = Camera.main;

		player = GetComponentInParent<Player>();

	}

	// Update is called once per frame
	void Update()
	{	
		Vector2 move = Vector2.zero;
		if (player != null)
		{
			move = player.moveInput;
		}
		else if (moveAction != null)
		{
			move = moveAction.ReadValue<Vector2>();
		}

		Vector3 dir = new Vector3(move.x, move.y, 0f);
		if (dir.sqrMagnitude > 0.0001f)
		{
			transform.up = dir.normalized;
		}
	}
}
