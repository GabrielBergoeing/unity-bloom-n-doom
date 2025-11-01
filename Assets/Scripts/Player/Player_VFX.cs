using System.Collections.Generic;
using UnityEngine;

public class Player_VFX : MonoBehaviour
{
    // Dictionary to store all particle effects by name
    private Dictionary<string, ParticleSystem> vfxLibrary;

    private void Awake()
    {
        vfxLibrary = new Dictionary<string, ParticleSystem>();

        // Automatically find all particle systems attached to the player or its children
        ParticleSystem[] effects = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var fx in effects)
        {
            string key = fx.gameObject.name.ToLower(); // use lowercase names for safe searching
            if (!vfxLibrary.ContainsKey(key))
                vfxLibrary.Add(key, fx);
        }
    }

    // Triggers a VFX by name (matching the GameObject name of the ParticleSystem)
    public void TriggerVFX(string effectName)
    {
        string key = effectName.ToLower();

        if (vfxLibrary.TryGetValue(key, out ParticleSystem fx))
        {
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fx.Play();
        }
        else
        {
            Debug.LogWarning($"No VFX named '{effectName}' found on player.");
        }
    }
}
