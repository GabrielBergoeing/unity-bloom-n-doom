using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class IconEntry
{
    public string buttonName;   // button identifier (ex: "A", "Cross", "L1", "DPadUp")
    public Sprite sprite;       // icon image
}

[Serializable]
public class DeviceIconSet
{
    public string deviceName;   // ex: "Xbox", "PlayStation", "Switch", "Generic"
    public List<IconEntry> icons = new List<IconEntry>();
}

[CreateAssetMenu(menuName = "UI/Gamepad Icons Database")]
public class GamepadIconsDatabase : ScriptableObject
{
    [Header("Icon Sets Per Device Type")]
    public List<DeviceIconSet> deviceSets = new List<DeviceIconSet>();

    private Dictionary<string, Dictionary<string, Sprite>> lookup;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        lookup = new();

        foreach (var set in deviceSets)
        {
            if (!lookup.ContainsKey(set.deviceName))
                lookup.Add(set.deviceName, new());

            foreach (var entry in set.icons)
            {
                if (!lookup[set.deviceName].ContainsKey(entry.buttonName))
                    lookup[set.deviceName].Add(entry.buttonName, entry.sprite);
            }
        }
    }

    public Sprite GetIcon(string deviceName, string buttonName)
    {
        if (lookup == null || lookup.Count == 0)
            BuildLookup();

        // Exact match
        if (lookup.TryGetValue(deviceName, out var map) &&
            map.TryGetValue(buttonName, out var sprite))
        {
            return sprite;
        }

        // Fallback to "Generic"
        if (deviceName != "Generic" &&
            lookup.TryGetValue("Generic", out var genericMap) &&
            genericMap.TryGetValue(buttonName, out var genericSprite))
        {
            return genericSprite;
        }

        Debug.LogWarning($"[GamepadIcons] Missing icon for {deviceName} → {buttonName}");
        return null;
    }
}
