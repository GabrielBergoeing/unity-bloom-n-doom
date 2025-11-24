using UnityEngine;
using UnityEngine.InputSystem;

public static class UIDeviceResolver
{
    public static InputDevice ResolveActiveDevice(PlayerInput player)
    {
        // Prefer LastUsedDevice from the service
        if (PlayerInputService.instance?.LastUsedDevice != null)
            return PlayerInputService.instance.LastUsedDevice;

        // A player exists → use their device
        if (player != null && player.devices.Count > 0)
            return player.devices[0];

        // Fallback for menus
        if (Keyboard.current != null) return Keyboard.current;
        if (Gamepad.current != null) return Gamepad.current;

        return null;
    }

    public static string ResolveDeviceType(InputDevice device)
    {
        // Identifying controls on Linux is a bit harder
        Debug.Log(
            $"[Device Info]\n" +
            $"name: {device.name}\n" +
            $"display: {device.displayName}\n" +
            $"manufacturer: {device.description.manufacturer}\n" +
            $"product: {device.description.product}\n" +
            $"serial: {device.description.serial}"
        );

        if (device == null) return "Generic";

        if (device is Keyboard) return "Keyboard";
        if (!(device is Gamepad)) return "Generic";

        // Extract all meaningful strings possible
        string layout = device.layout?.ToLower() ?? "";
        string name = device.name?.ToLower() ?? "";
        string display = device.displayName?.ToLower() ?? "";
        string product = device.description.product?.ToLower() ?? "";
        string manufacturer = device.description.manufacturer?.ToLower() ?? "";
        string serial = device.description.serial?.ToLower() ?? "";

        // PLAYSTATION: product/manufacturer/serial detection
        if (layout.Contains("dualshock") || layout.Contains("dualsense") ||
            name.Contains("ps") || name.Contains("sony") ||
            display.Contains("ps") || display.Contains("sony") ||
            product.Contains("ps") || product.Contains("playstation") ||
            product.Contains("dualshock") || product.Contains("dualsense"))
            return "PlayStation";

        // NINTENDO SWITCH
        if (layout.Contains("switch") ||
            name.Contains("joycon") || name.Contains("nintendo") ||
            display.Contains("joycon") || display.Contains("nintendo") ||
            product.Contains("joycon") || product.Contains("nintendo"))
            return "Switch";

        // XBOX
        if (layout.Contains("xinput") ||
            name.Contains("xbox") || manufacturer.Contains("microsoft") ||
            product.Contains("xbox") || display.Contains("xbox"))
            return "XboxController";

        // Generic Gamepad fallback
        return "Gamepad";
    }


    public static string ExtractButtonName(string bindingPath)
    {
        if (string.IsNullOrEmpty(bindingPath)) return "";
        int i = bindingPath.LastIndexOf('/');
        return i >= 0 ? bindingPath[(i + 1)..] : bindingPath;
    }
}
