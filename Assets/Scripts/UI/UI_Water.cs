using UnityEngine;
using UnityEngine.UI;

public class UI_Water : MonoBehaviour
{
    private Slider waterSlider;
    
    // Reference to the Player's main script (e.g., Player.cs)
    private Player playerScript; 

    void Start()
    {
        waterSlider = GetComponent<Slider>();

        // Find the Player script on the parent GameObject.
        playerScript = GetComponentInParent<Player>(); 

        if (playerScript == null)
        {
            Debug.LogError("Player script not found on the parent!");
            return;
        }

        waterSlider.maxValue = 100;
        waterSlider.minValue = 0;
    }

    void Update()
    {
        if (playerScript != null && waterSlider != null)
        {
            // Directly read the public attribute from the Player script
            waterSlider.value = playerScript.waterSupply;
        }
    }
}