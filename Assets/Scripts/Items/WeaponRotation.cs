using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponRotation : MonoBehaviour
{
	Camera mainCamera;
	Player player;
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
			// player.moveInput is never synced (server-only field, no [SyncVar]) - it only
			// ever holds real data on whichever instance is actually the server, so every
			// client (including the one holding this weapon) always read it as zero/stale,
			// causing rotation to desync from what the host actually sees. xFacingDir/
			// yFacingDir are SyncVars and already drive animation facing, so they're
			// replicated correctly to everyone.
			move = new Vector2(player.xFacingDir, player.yFacingDir);
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
