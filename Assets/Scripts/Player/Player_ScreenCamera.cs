using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class Player_ScreenCamera : MonoBehaviour
{
    private Camera cam;
    private PlayerInput player;  // Now stored
    private int index;
    private int totalPlayers;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        // Wait for PlayerInput to exist
        StartCoroutine(WaitForPlayerInput());

        // Update camera layout when new players join
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.onPlayerJoined += HandlePlayerJoined;
    }

    private System.Collections.IEnumerator WaitForPlayerInput()
    {
        // Wait until PlayerInput exists on parent
        while (player == null)
        {
            player = GetComponentInParent<PlayerInput>();
            yield return null;
        }

        index = player.playerIndex;
        totalPlayers = PlayerInput.all.Count;
        cam.depth = index;

        SetupCamera();
    }

    private void HandlePlayerJoined(PlayerInput obj)
    {
        totalPlayers = PlayerInput.all.Count;
        SetupCamera();
    }

    private void SetupCamera()
    {
        if (totalPlayers <= 0 || cam == null) return;

        if (totalPlayers == 1)
            cam.rect = new Rect(0, 0, 1, 1);
        else if (totalPlayers == 2)
            cam.rect = new Rect(index == 0 ? 0 : 0.5f, 0, 0.5f, 1);
        else if (totalPlayers == 3)
            cam.rect = new Rect(
                index == 0 ? 0 : (index == 1 ? 0.5f : 0),
                index < 2 ? 0.5f : 0,
                index < 2 ? 0.5f : 1,
                0.5f
            );
        else
            cam.rect = new Rect(
                (index % 2) * 0.5f,
                index < 2 ? 0.5f : 0,
                0.5f,
                0.5f
            );
    }
}
