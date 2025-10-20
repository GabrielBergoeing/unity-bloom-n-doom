using UnityEngine;

public class Plant : MonoBehaviour
{
    public enum GrowthStage { Seed, Growing, Mature }

    [Header("Owner / Grid")]
    public int ownerPlayerIndex = -1;
    public Vector3Int cellPos;

    [Header("Growth")]
    [Tooltip("Cuántas interacciones (riegos) necesita para madurar")]
    public int interactionsToMature = 2;
    public int currentInteractions = 0;
    public GrowthStage stage = GrowthStage.Seed;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public Sprite seedSprite;
    public Sprite growingSprite;
    public Sprite matureSprite;

    
    public void Init(int ownerIndex, Vector3Int gridCell, int? requiredInteractions = null)
    {
        ownerPlayerIndex = ownerIndex;
        cellPos = gridCell;

        if (requiredInteractions.HasValue)
            interactionsToMature = Mathf.Max(0, requiredInteractions.Value);

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        if (interactionsToMature == 0)
            SetStage(GrowthStage.Mature);
        else
            SetStage(GrowthStage.Seed);
    }

    public void Interact()
    {
        if (stage == GrowthStage.Mature) return;

        currentInteractions++;

        if (currentInteractions >= interactionsToMature)
        {
            SetStage(GrowthStage.Mature);
        }
        else if (stage == GrowthStage.Seed)
        {
            SetStage(GrowthStage.Growing);
        }
    }

    private void SetStage(GrowthStage newStage)
    {
        stage = newStage;

        // Sprites por etapa
        if (spriteRenderer)
        {
            var s = stage switch
            {
                GrowthStage.Seed    => seedSprite    ? seedSprite    : spriteRenderer.sprite,
                GrowthStage.Growing => growingSprite ? growingSprite : spriteRenderer.sprite,
                GrowthStage.Mature  => matureSprite  ? matureSprite  : spriteRenderer.sprite,
                _ => spriteRenderer.sprite
            };
            spriteRenderer.sprite = s;
        }

    
    }
}
