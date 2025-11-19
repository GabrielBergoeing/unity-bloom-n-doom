using UnityEngine.InputSystem;

public static class PlayerInputExtensions
{
    public static bool OwnsDevice(this PlayerInput p, InputDevice device)
    {
        var devices = p.devices;

        for (int i = 0; i < devices.Count; i++)
            if (devices[i] == device)
                return true;

        return false;
    }
}
