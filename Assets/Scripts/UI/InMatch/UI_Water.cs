using UnityEngine;
using UnityEngine.UI;

public class UI_Water : MonoBehaviour
{
    private Slider slider;
    private Player playerScript; 

    void Start()
    {
        slider = GetComponent<Slider>();
        playerScript = GetComponentInParent<Player>(); 

        if (playerScript == null)
        {
            Debug.LogError("Player script not found on the parent!");
            return;
        }

        slider.maxValue = 100;
        slider.minValue = 0;

        Debug.Log($"[UI_Water] Bound to player '{playerScript.name}' (isLocalPlayer={playerScript.isLocalPlayer} OwnerIndex={playerScript.OwnerIndex})");
    }

    void Update()
    {
        if (playerScript != null && slider != null)
        {
            slider.value = playerScript.waterSupply;
        }
    }
}