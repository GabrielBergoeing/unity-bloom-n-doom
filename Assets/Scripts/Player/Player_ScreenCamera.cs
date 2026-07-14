using UnityEngine;

/// <summary>
/// Per-player camera. Online there is no split screen: the local player's camera
/// renders full screen and remote players' cameras are disabled entirely
/// (NetworkPlayer calls ConfigureAsLocal / DisableAsRemote on spawn).
/// </summary>
[RequireComponent(typeof(Camera))]
public class Player_ScreenCamera : MonoBehaviour
{
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    public void ConfigureAsLocal()
    {
        cam.rect = new Rect(0f, 0f, 1f, 1f);
        cam.depth = 10f; // render above any leftover scene camera
        cam.enabled = true;

        var myListener = GetComponent<AudioListener>();
        if (myListener != null) myListener.enabled = true;

        // Unity wants exactly one active listener; mute the scene camera's.
        foreach (var other in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (other != myListener && other.enabled)
                other.enabled = false;
        }
    }

    public void DisableAsRemote()
    {
        cam.enabled = false;

        var listener = GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;
    }
}
