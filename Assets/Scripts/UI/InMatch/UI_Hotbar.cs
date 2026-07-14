using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Renders the local player's id-based hotbar. Item sprites are resolved from the
/// item prefabs registered in NetworkAssets (itemId == index).
/// </summary>
public class UI_Hotbar : MonoBehaviour
{
    public HotbarSystem hotbarSystem;
    public Transform[] uiSlots = new Transform[4];
    private Image[] slotImages;
    public TextMeshProUGUI[] stackCountTexts;

    private readonly Dictionary<int, Sprite> spriteCache = new();

    private void Start()
    {
        // Setup UI transforms safely
        uiSlots = new Transform[4];
        stackCountTexts = new TextMeshProUGUI[4];
        slotImages = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            uiSlots[i] = transform.Find("Slot" + i);
            stackCountTexts[i] = uiSlots[i].Find("StackCount").GetComponent<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        if (hotbarSystem != null && slotImages != null)
            UpdateUISlots();
    }

    public void UpdateUISlots()
    {
        for (int i = 0; i < 4; i++)
        {
            int itemId = hotbarSystem.GetSlotItemId(i);

            if (itemId >= 0)
            {
                Sprite sprite = ResolveSprite(itemId);

                if (sprite != null)
                {
                    if (slotImages[i] == null)
                    {
                        GameObject imageObj = new GameObject("ItemImage");
                        imageObj.transform.SetParent(uiSlots[i], false);
                        imageObj.transform.SetAsFirstSibling();
                        slotImages[i] = imageObj.AddComponent<Image>();
                    }
                    slotImages[i].sprite = sprite;
                    slotImages[i].enabled = true;
                }

                var prefab = NetworkAssets.Instance != null ? NetworkAssets.Instance.GetItemPrefab(itemId) : null;
                var pickup = prefab != null ? prefab.GetComponent<Pickup>() : null;
                int count = hotbarSystem.GetStackCount(i);

                if (pickup != null && pickup.stackable && count > 1)
                {
                    stackCountTexts[i].text = count.ToString();
                    stackCountTexts[i].gameObject.SetActive(true);
                }
                else
                {
                    stackCountTexts[i].gameObject.SetActive(false);
                }
            }
            else
            {
                if (slotImages[i] != null)
                    slotImages[i].enabled = false;

                stackCountTexts[i].gameObject.SetActive(false);
            }
        }
    }

    private Sprite ResolveSprite(int itemId)
    {
        if (spriteCache.TryGetValue(itemId, out var cached))
            return cached;

        var prefab = NetworkAssets.Instance != null ? NetworkAssets.Instance.GetItemPrefab(itemId) : null;
        Sprite sprite = null;

        if (prefab != null)
        {
            var sr = prefab.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
            {
                foreach (var childSr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (childSr.gameObject.name == "StackCount") continue;
                    if (childSr.sprite == null) continue;
                    sr = childSr;
                    break;
                }
            }
            if (sr != null) sprite = sr.sprite;
        }

        spriteCache[itemId] = sprite;
        return sprite;
    }

    public void AssignHotbar(HotbarSystem system)
    {
        hotbarSystem = system;
    }
}
