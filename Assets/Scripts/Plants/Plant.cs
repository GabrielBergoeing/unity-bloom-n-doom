using Mirror;
using UnityEngine;

// Para la entrega final habra que hacer la clase planta ser heredado desde Entity
public class Plant : NetworkBehaviour
{
    public enum GrowthStage { Seed, Growing, Mature }
    protected Rigidbody2D rb;

    [Header("Owner / Grid")]
    [SyncVar] public int ownerPlayerIndex = -1;
    [SyncVar] public Vector3Int cellPos = default;

    [Header("Growth")]
    [Tooltip("Cuántas interacciones (riegos) necesita para madurar")]
    [SerializeField] protected int interactionsToMature = 2;
    [SyncVar(hook = nameof(OnInteractionsChanged))] public int currentInteractions = 0;
    [SyncVar(hook = nameof(OnStageChanged))] public GrowthStage stage = GrowthStage.Seed;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public Sprite seedSprite;
    public Sprite growingSprite;
    public Sprite matureSprite;

    [Header("Health and Withering time (in seconds)")]
    [Range(0, 20)][SerializeField] protected float maxHealth = 10;
    [Range(0f, 60f)][SerializeField] protected float witheringTime = 30f;
    [SyncVar(hook = nameof(OnTimerChanged))] private float timerSync = 0f; // sincroniza resets de timer
    protected float health;
    protected float timer;

    [Header("Fire System")]
    [Range(0.1f, 10f)][SerializeField] private float fireDamagePerSecond = 0.5f;
    [Range(0.1f, 30f)][SerializeField] private float fireDuration = float.MaxValue;
    [Range(0.5f, 5f)][SerializeField] private float fireFlickerSpeed = 1f;
    [SerializeField] private bool burnUntilDeath = true;
    [SyncVar(hook = nameof(OnFireChanged))] public bool isOnFire = false;
    private float fireTimer = 0f;
    private Color originalColor;

    [Header("Scoring")]
    [Range(0, 5)][SerializeField] private int score = 3;

    // small optimization: only push timerSync to clients cada 1s
    private float lastTimerSyncPush = 0f;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    #region Server Initialization & lifecycle
    // Server-side init
    [Server]
    public void InitServer(int ownerIndex, Vector3Int gridCell, int? requiredInteractions = null)
    {
        ownerPlayerIndex = ownerIndex;
        cellPos = gridCell;

        if (requiredInteractions.HasValue)
            interactionsToMature = Mathf.Max(1, requiredInteractions.Value); // force at least 1
        else
            interactionsToMature = Mathf.Max(1, interactionsToMature); // ensure prefab can't be 0

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        stage = GrowthStage.Seed;
        currentInteractions = 0;
        health = maxHealth;
        timer = witheringTime;
        timerSync = timer;
        isOnFire = false;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // ensure server values are set even if InitServer wasn't called (fallback)
        if (health <= 0) health = maxHealth;
        if (timer <= 0) timer = witheringTime;
        timerSync = timer;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // register plant on client FarmManager so client-side queries (IsOccupied, HasPlant) work
        if (FarmManager.instance != null)
            FarmManager.instance.RegisterClientPlant(this);
        // update visuals to reflect SyncVar values
        UpdateStageVisual(stage);
        UpdateFireVisuals(isOnFire);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (FarmManager.instance != null)
            FarmManager.instance.UnregisterClientPlant(this);
    }
    #endregion

    #region SyncVar hooks -> update visuals on clients
    void OnStageChanged(GrowthStage oldStage, GrowthStage newStage)
    {
        UpdateStageVisual(newStage);
    }

    void OnInteractionsChanged(int oldVal, int newVal)
    {
        // optional: could update UI or play sound
    }

    void OnFireChanged(bool oldVal, bool newVal)
    {
        UpdateFireVisuals(newVal);
    }

    void OnTimerChanged(float oldVal, float newVal)
    {
        // timer sync used for wither visuals; UpdateDarkening reads GetWitherRatio which
        // on clients uses timerSync now.
        timerSync = newVal;
    }
    #endregion

    #region Visual updates
    private void UpdateStageVisual(GrowthStage newStage)
    {
        stage = newStage;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer)
        {
            var s = newStage switch
            {
                GrowthStage.Seed => seedSprite ? seedSprite : spriteRenderer.sprite,
                GrowthStage.Growing => growingSprite ? growingSprite : spriteRenderer.sprite,
                GrowthStage.Mature => matureSprite ? matureSprite : spriteRenderer.sprite,
                _ => spriteRenderer.sprite
            };
            spriteRenderer.sprite = s;
        }
    }

    private void UpdateFireVisuals(bool on)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (on)
        {
            if (originalColor == Color.clear || originalColor == default(Color))
                originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            // VFX handled by Plant_VFX and Particle prefabs if present
        }
        else
        {
            if (spriteRenderer != null && health > 0)
                spriteRenderer.color = originalColor;
        }
    }
    #endregion

    protected virtual void Update()
    {
        // Server executes game logic: timer, fire damage, die
        if (isServer)
        {
            // decrease timer and sync occasionally
            timer -= Time.deltaTime;

            if (isOnFire)
            {
                fireTimer -= Time.deltaTime;
                TakeDamage(fireDamagePerSecond * Time.deltaTime);

                if (!burnUntilDeath && fireTimer <= 0f)
                {
                    ExtinguishFire();
                }
            }

            // push timerSync every 1s to clients to allow client visuals
            if (Time.time - lastTimerSyncPush >= 1f)
            {
                timerSync = timer;
                lastTimerSyncPush = Time.time;
            }

            if (timer <= 0 || health <= 0)
            {
                Die();
            }
        }
    }

    #region Game actions (server-side)
    [Server]
    public void Interact()
    {
        if (IsFullyGrown()) return;

        currentInteractions++;

        if (currentInteractions >= interactionsToMature)
            stage = GrowthStage.Mature;
        else if (stage == GrowthStage.Seed)
            stage = GrowthStage.Growing;
    }

    [Server]
    public void WaterPlant()
    {
        if (isOnFire)
        {
            ExtinguishFire();
        }
        timer = witheringTime;
        timerSync = timer;

        if (stage == GrowthStage.Mature) return;

        currentInteractions++;

        if (currentInteractions >= interactionsToMature)
            stage = GrowthStage.Mature;
        else if (stage == GrowthStage.Seed)
            stage = GrowthStage.Growing;
    }

    [Server]
    public void FertilizePlant()
    {
        if (stage == GrowthStage.Mature) return;

        currentInteractions = interactionsToMature;
        stage = GrowthStage.Mature;
        timer = witheringTime;
        timerSync = timer;
    }

    [Server]
    public void SetOnFire()
    {
        if (isOnFire) return;
        isOnFire = true;
        fireTimer = fireDuration;
    }

    [Server]
    public void ExtinguishFire()
    {
        isOnFire = false;
        fireTimer = 0f;
    }

    public virtual void TakeDamage(float damage)
    {
        if (!isServer) return;
        health -= damage;
    }

    [Server]
    private void Die()
    {
        // notify FarmManager server-side
        if (FarmManager.instance != null)
        {
            // compute cell from position on server if needed - FarmManager server tracks plantsByCell
            FarmManager.instance.NotifyPlantDeath(FarmManager.instance.farmTilemap.WorldToCell(transform.position));
        }

        NetworkServer.Destroy(gameObject);
    }
    #endregion

    protected bool IsFullyGrown() => stage == GrowthStage.Mature;

    public virtual int GetScoring()
    {
        return stage switch
        {
            GrowthStage.Seed => 0,
            GrowthStage.Growing => Mathf.CeilToInt(score * 0.5f),
            GrowthStage.Mature => score,
            _ => 0
        };
    }

    // Used by client VFX to compute wither ratio (uses last synced timer)
    public float GetWitherRatio()
    {
        // use timerSync on clients, server uses timer
        float t = isServer ? timer : timerSync;
        return Mathf.Clamp01(t / witheringTime);
    }
}
