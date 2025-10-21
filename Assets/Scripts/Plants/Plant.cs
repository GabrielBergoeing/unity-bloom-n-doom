using UnityEngine;

//Para la entrega final habra que hacer la clase planta ser heredado desde Entity
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

    [Header("Health and Withering time (in seconds)")]
    [Range(0, 20)][SerializeField] private float maxHealth = 10;
    [Range(0f, 60f)][SerializeField] private float witheringTime = 30f;
    private float health;
    private float timer;

    [Header("Scoring")]
    [Range(0, 5)][SerializeField] private int score = 3;

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

    private void SetStage(GrowthStage newStage)
    {
        stage = newStage;

        // Sprites por etapa
        if (spriteRenderer)
        {
            var s = stage switch
            {
                GrowthStage.Seed => seedSprite ? seedSprite : spriteRenderer.sprite,
                GrowthStage.Growing => growingSprite ? growingSprite : spriteRenderer.sprite,
                GrowthStage.Mature => matureSprite ? matureSprite : spriteRenderer.sprite,
                _ => spriteRenderer.sprite
            };
            spriteRenderer.sprite = s;
        }

        health = maxHealth;
        timer = witheringTime;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0 || health <= 0)
            Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    public void Interact()
    {
        if (stage == GrowthStage.Mature) return;

        currentInteractions++;

        if (currentInteractions >= interactionsToMature)
            SetStage(GrowthStage.Mature);
        else if (stage == GrowthStage.Seed)
            SetStage(GrowthStage.Growing);
    }

    public void WaterPlant()
    {
        timer = witheringTime;

        if (stage == GrowthStage.Mature) return;

        currentInteractions++;

        if (currentInteractions >= interactionsToMature)
            SetStage(GrowthStage.Mature);
        else if (stage == GrowthStage.Seed)
            SetStage(GrowthStage.Growing);
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
    }

    public int GetScoring()
    {
        return stage switch
        {
            GrowthStage.Seed => 0,
            GrowthStage.Growing => Mathf.CeilToInt(score * 0.5f),
            GrowthStage.Mature => score,
            _ => 0
        };
    }

    public float GetWitherRatio() => Mathf.Clamp01(timer / witheringTime);
}
