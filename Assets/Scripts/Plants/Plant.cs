using UnityEngine;
using Mirror;

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
    // Range raised from 60 - players were complaining plants died too fast, so every plant
    // prefab's witheringTime got bumped up (~x1.5) and 60 was already the old ceiling.
    [Range(0f, 90f)][SerializeField] protected float witheringTime = 45f;
    [SyncVar(hook = nameof(OnTimerChanged))] private float timerSync = 0f; // sincroniza resets de timer
    protected float health;
    protected float timer;

    [Header("Fire System")]
    [Range(0.1f, 10f)][SerializeField] private float fireDamagePerSecond = 0.5f;
    [Range(0.1f, 30f)][SerializeField] private float fireDuration = float.MaxValue;
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
    // Runs directly offline; server-authoritative online.
    public void InitServer(int ownerIndex, Vector3Int gridCell, int? requiredInteractions = null)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        ownerPlayerIndex = ownerIndex;
        cellPos = gridCell;

        if (requiredInteractions.HasValue)
            interactionsToMature = Mathf.Max(1, requiredInteractions.Value); // force at least 1
        else
            interactionsToMature = Mathf.Max(1, interactionsToMature); // ensure prefab can't be 0

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        stage = GrowthStage.Seed;
        // stage is already GrowthStage.Seed by default, so the SyncVar hook above won't fire
        // (Mirror only invokes hooks on an actual value change) - update the sprite directly
        // or it stays on whatever was serialized on the prefab (often the mature sprite).
        UpdateStageVisual(GrowthStage.Seed);
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
        // Offline: this is the only simulation, runs unconditionally.
        // Online: only the server simulates; clients just see synced state.
        bool isNetworkSpawnedObject = isServer || isClient;
        if (!isNetworkSpawnedObject || isServer)
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

    #region Game actions (server-side online, direct offline)
    public void Interact()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        if (IsFullyGrown()) return;

        currentInteractions++;

        if (currentInteractions >= interactionsToMature)
            stage = GrowthStage.Mature;
        else if (stage == GrowthStage.Seed)
            stage = GrowthStage.Growing;

        // Mirror's SyncVar hooks only auto-fire when NetworkServer.activeHost is true,
        // so offline (no active Mirror session) OnStageChanged never runs - update the
        // sprite directly instead of relying on it.
        UpdateStageVisual(stage);
    }

    public void WaterPlant()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

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

        UpdateStageVisual(stage);
    }

    public void FertilizePlant()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        if (stage == GrowthStage.Mature) return;

        currentInteractions = interactionsToMature;
        stage = GrowthStage.Mature;
        timer = witheringTime;
        timerSync = timer;

        UpdateStageVisual(stage);
    }

    public void SetOnFire()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        if (isOnFire) return;
        isOnFire = true;
        fireTimer = fireDuration;
        UpdateFireVisuals(isOnFire);
    }

    public void ExtinguishFire()
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        isOnFire = false;
        fireTimer = 0f;
        UpdateFireVisuals(isOnFire);
    }

    public virtual void TakeDamage(float damage)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        health -= damage;
    }

    private void Die()
    {
        // notify FarmManager
        if (FarmManager.instance != null)
        {
            FarmManager.instance.NotifyPlantDeath(FarmManager.instance.farmTilemap.WorldToCell(transform.position));
        }

        // NetworkServer.Destroy() is a no-op without an active server, so fall back
        // to a plain Destroy() offline or the plant object would never disappear.
        if (isServer)
            NetworkServer.Destroy(gameObject);
        else
            Destroy(gameObject);
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
        // whoever simulates (offline, or the server online) has the authoritative 'timer';
        // true remote clients only have the periodically synced 'timerSync'
        bool isNetworkSpawnedObject = isServer || isClient;
        float t = (!isNetworkSpawnedObject || isServer) ? timer : timerSync;
        return Mathf.Clamp01(t / witheringTime);
    }
}
