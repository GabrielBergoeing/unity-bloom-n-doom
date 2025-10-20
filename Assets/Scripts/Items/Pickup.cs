using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.InputSystem;

//This script allows the player to pick up and drop items, is intended only for sabotage itmes such as the flamethrower
public class Pickup : MonoBehaviour
{
    public bool canPickup = false;

    public bool isPickedUp = false;
    public bool stackable = false; //Indicates if the item can be stacked in the hotbar
    public int maxStackCount = 5; // The maximum stack count of the item

    public int itemId; // Unique identifier for the item type
    public PlayerInput playerInput;
    public Player currentPlayer;
    public HotbarSystem hotbarSystem;
    private Player savedPlayer;
    private HotbarSystem savedHotbarSystem;
    private PlayerInput savedPlayerInput;

    // Timestamp until which this item cannot be picked up (Time.time)
    public float unpickupableUntil = 0f;
    public float throwCooldown = 10f;

    // New: visual fade when thrown
    [SerializeField] private float thrownFadeDuration = 10f; // seconds
    private float thrownFadeTimer = 0f;
    private bool isThrownVisual = false;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;

    void Start()
    {
        // Initialize sprite renderers and save original colors
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            originalColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                originalColors[i] = spriteRenderers[i].color;
            }
        }
    }
    void Update()
    {
        if (playerInput != null && playerInput.actions["Pickup"].triggered)
        {
            if (canPickup)
            {
                PickupItem();
            }
        }
        if (playerInput != null && playerInput.actions["Drop"].triggered)
        {
            if (isPickedUp)
            {
                DropItem();
            }
        }

        // Handle thrown visual fade
        if (isThrownVisual)
        {
            thrownFadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(thrownFadeTimer / thrownFadeDuration);
            if (spriteRenderers != null && originalColors != null)
            {
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i] != null)
                    {
                        spriteRenderers[i].color = Color.Lerp(Color.black, originalColors[i], t);
                    }
                }
            }
            if (t >= 1f)
            {
                // finished fading
                isThrownVisual = false;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If currently unpickable, ignore player triggers
        if (Time.time < unpickupableUntil) return;

        if (other.CompareTag("Player"))
        {
            currentPlayer = other.GetComponent<Player>();
            playerInput = currentPlayer.GetComponent<PlayerInput>();
            hotbarSystem = currentPlayer.GetComponent<HotbarSystem>();
            canPickup = true;
            savedPlayer = currentPlayer;
            savedHotbarSystem = hotbarSystem;
            savedPlayerInput = playerInput;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isPickedUp)
        {
            currentPlayer = null;
            playerInput = null;
            hotbarSystem = null;
            canPickup = false;
        }
    }
    private void PickupItem()
    {
        if (hotbarSystem != null)
        {
            if (hotbarSystem.AddItem(gameObject))
            {
                canPickup = false;
                currentPlayer = savedPlayer;
                hotbarSystem = savedHotbarSystem;
                playerInput = savedPlayerInput;
                isPickedUp = true;
                // Restore visuals if we were mid-fade
                RestoreOriginalColors();

                if (GetComponent<Collider2D>() != null)
                {
                    GetComponent<Collider2D>().enabled = false;
                }
            }
        }
    }


    public void DropItem(bool consume = false, bool thrown = false)
    {
        if (hotbarSystem != null)
        {
            if (consume)
            {

                if (hotbarSystem.stackCounts[hotbarSystem.currentSlot] == 1)
                {
                    isPickedUp = false;
                    hotbarSystem.RemoveItem(gameObject);
                    Destroy(gameObject);
                }
                else
                {
                    hotbarSystem.RemoveItem(gameObject);
                }
                return;
            }

            if (hotbarSystem.stackCounts[hotbarSystem.currentSlot] == 1)
            {
                isPickedUp = false;
                transform.SetParent(null);
                hotbarSystem.RemoveItem(gameObject);
                gameObject.SetActive(true);
            }
            else
            {
                hotbarSystem.RemoveItem(gameObject);
            }

            if (thrown)
            {
                unpickupableUntil = Time.time + throwCooldown;
                currentPlayer = null;
                playerInput = null;
                hotbarSystem = null;
                canPickup = false;

                // Start thrown visual: set all sprites to black and begin fade
                thrownFadeTimer = 0f;
                isThrownVisual = true;
                if (spriteRenderers != null)
                {
                    for (int i = 0; i < spriteRenderers.Length; i++)
                    {
                        if (spriteRenderers[i] != null)
                        {
                            spriteRenderers[i].color = Color.black;
                        }
                    }
                }
            }

            if (GetComponent<Collider2D>() != null)
            {
                GetComponent<Collider2D>().enabled = true;
            }
        }
    }

    // New helper to restore saved colors immediately (e.g., when picked up)
    private void RestoreOriginalColors()
    {
        isThrownVisual = false;
        thrownFadeTimer = 0f;
        if (spriteRenderers != null && originalColors != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].color = originalColors[i];
                }
            }
        }
    }
}
