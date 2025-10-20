using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_Hotbar : MonoBehaviour
{
    public HotbarSystem hotbarSystem;
    public Transform[] uiSlots = new Transform[4];
    private Image[] slotImages;
    public TextMeshProUGUI[] stackCountTexts;

    void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            uiSlots[i] = transform.Find("Slot" + i);
        }
        slotImages = new Image[4];
        stackCountTexts = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            stackCountTexts[i] = uiSlots[i].Find("StackCount").GetComponent<TextMeshProUGUI>();
        }
        UpdateUISlots();
    }
    void Update()
    {
        UpdateUISlots();
    }

    public void UpdateUISlots()
    {
        for (int i = 0; i < hotbarSystem.slots.Length; i++)
        {
            if (hotbarSystem.slots[i] != null)
            {
                SpriteRenderer itemSprite = hotbarSystem.slots[i].GetComponent<SpriteRenderer>();
                if (itemSprite != null)
                {
                    if (slotImages[i] == null)
                    {
                        GameObject imageObj = new GameObject("ItemImage");
                        imageObj.transform.SetParent(uiSlots[i], false);
                        imageObj.transform.SetAsFirstSibling();
                        slotImages[i] = imageObj.AddComponent<Image>();
                    }
                    slotImages[i].sprite = itemSprite.sprite;
                    slotImages[i].enabled = true;
                    Pickup pickup = hotbarSystem.slots[i].GetComponent<Pickup>();
                    if (pickup != null && pickup.stackable && hotbarSystem.stackCounts[i] > 1)
                    {
                        stackCountTexts[i].text = hotbarSystem.stackCounts[i].ToString();
                        stackCountTexts[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        stackCountTexts[i].gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                if (slotImages[i] != null)
                {
                    slotImages[i].enabled = false;
                }
                stackCountTexts[i].gameObject.SetActive(false);
            }
        }
    }
}
