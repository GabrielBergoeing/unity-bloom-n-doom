using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class Player_ScreenCamera : MonoBehaviour
{
    private Camera cam;
    private Player ownerPlayer;
    private PlayerInput player;  // Now stored
    private int index;
    private int totalPlayers;
    private bool isSubscribed;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ownerPlayer = GetComponentInParent<Player>();

        // Wait for PlayerInput to exist
        StartCoroutine(WaitForPlayerInput());

        // Update camera layout when new players join
        if (PlayerInputManager.instance != null)
        {
            PlayerInputManager.instance.onPlayerJoined += HandlePlayerJoined;
            isSubscribed = true;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromPlayerJoined();
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayerJoined();
    }

    private void UnsubscribeFromPlayerJoined()
    {
        if (!isSubscribed) return;

        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.onPlayerJoined -= HandlePlayerJoined;

        isSubscribed = false;
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

        if (cam != null)
            cam.depth = index;

        SetupCamera();
    }

    private void Update()
    {
        RefreshOnlineCameraOwnership();
    }

    private void RefreshOnlineCameraOwnership()
    {
        if (cam == null)
            return;

        bool isOnlineSession = NetworkClient.active || NetworkServer.active;
        if (!isOnlineSession)
        {
            if (!cam.enabled)
                cam.enabled = true;
            return;
        }

        bool shouldEnable = ownerPlayer != null && ownerPlayer.isLocalPlayer;
        if (cam.enabled != shouldEnable)
            cam.enabled = shouldEnable;

        if (shouldEnable)
            cam.rect = new Rect(0, 0, 1, 1);
    }

    private void HandlePlayerJoined(PlayerInput obj)
    {
        if (this == null || cam == null)
            return;

        if (!cam.enabled)
            return;

        totalPlayers = PlayerInput.all.Count;
        bool isOnlineSession = NetworkServer.active || NetworkClient.active;

        if (isOnlineSession)
        {
            // Online — each client sees only their own player fullscreen
            cam.rect = new Rect(0, 0, 1, 1);
        }
        else
        {
            // Local — split screen based on player count
            SetupCamera();
        }
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
