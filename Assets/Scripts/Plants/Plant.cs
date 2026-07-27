using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Online: spawned by the server as a NetworkObject. The server simulates growth,
/// withering, fire and health; clients only render state mirrored through
/// NetworkVariables (stage, fire, wither ratio) + an init RPC (owner, cell).
/// </summary>
public class Plant : NetworkBehaviour
{
    public enum GrowthStage { Seed, Growing, Mature }
    protected Rigidbody2D rb;

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
    [Range(0, 20)][SerializeField] protected float maxHealth = 10;
    [Range(0f, 90f)][SerializeField] protected float witheringTime = 45f;
    protected float health;
    protected float timer;

    [Header("Fire System")]
    [Range(0.1f, 10f)][SerializeField] private float fireDamagePerSecond = 0.5f;
    [Range(0.1f, 30f)][SerializeField] private float fireDuration = float.MaxValue;
    [Range(0.5f, 5f)][SerializeField] private float fireFlickerSpeed = 1f;
    [SerializeField] private bool burnUntilDeath = true;
    [HideInInspector] public bool isOnFire = false;
    private float fireTimer = 0f;
    private Color originalColor;

    [Header("Scoring")]
    [Range(0, 5)][SerializeField] private int score = 3;

    // ---------------- Netcode ----------------
    public NetworkVariable<int> netStage = new(0);
    public NetworkVariable<bool> netFire = new(false);
    public NetworkVariable<float> netWither = new(1f);

    private float witherSyncTimer;
    private bool isDying;

    /// <summary>True when this plant is a live networked object.</summary>
    protected bool IsNetworked => GameSession.OnlineActive && IsSpawned;

    /// <summary>True on the peer that simulates this plant (server online, everyone offline).</summary>
    protected bool IsSimAuthority => !IsNetworked || IsServer;

    protected virtual void Awake() => rb = GetComponent<Rigidbody2D>();

    // ======================================================
    //  INIT
    // ======================================================
    /// <summary>Server-side init, called right before Spawn().</summary>
    public void ServerPrepareInit(int ownerIndex, Vector3Int gridCell)
    {
        ownerPlayerIndex = ownerIndex;
        cellPos = gridCell;
    }

    public override void OnNetworkSpawn()
    {
        netStage.OnValueChanged += OnStageChanged;
        netFire.OnValueChanged += OnFireChanged;

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        if (IsServer)
        {
            Init(ownerPlayerIndex, cellPos);
        }
        else
        {
            ApplyStageVisual((GrowthStage)netStage.Value);
            if (netFire.Value) OnFireChanged(false, true);
        }
    }

    public override void OnNetworkDespawn()
    {
        netStage.OnValueChanged -= OnStageChanged;
        netFire.OnValueChanged -= OnFireChanged;
        FarmManager.instance?.UnregisterPlant(this);
    }

    [ClientRpc]
    public void InitClientRpc(int ownerIndex, Vector3Int gridCell)
    {
        ownerPlayerIndex = ownerIndex;
        cellPos = gridCell;
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        FarmManager.instance?.RegisterPlant(this);
    }

    public void Init(int ownerIndex, Vector3Int gridCell, int? requiredInteractions = null)
    {
        ownerPlayerIndex = ownerIndex;
        cellPos = gridCell;

        if (requiredInteractions.HasValue)
            interactionsToMature = Mathf.Max(1, requiredInteractions.Value); // force at least 1
        else
            interactionsToMature = Mathf.Max(1, interactionsToMature); // ensure prefab can't be 0

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        SetStage(GrowthStage.Seed);
    }

    // ======================================================
    //  STAGE
    // ======================================================
    private void SetStage(GrowthStage newStage)
    {
        ApplyStageVisual(newStage);

        health = maxHealth;
        timer = witheringTime;

        if (IsNetworked && IsServer)
            netStage.Value = (int)newStage;
    }

    private void ApplyStageVisual(GrowthStage newStage)
    {
        stage = newStage;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

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
    }

    private void OnStageChanged(int oldValue, int newValue) =>
        ApplyStageVisual((GrowthStage)newValue);

    // ======================================================
    //  SIMULATION
    // ======================================================
    protected virtual void Update()
    {
        if (IsNetworked && !IsServer)
        {
            // Clients only animate the fire flicker; state comes from the server.
            if (isOnFire)
                UpdateFireVisuals();
            return;
        }

        timer -= Time.deltaTime;

        if (isOnFire)
        {
            fireTimer -= Time.deltaTime;
            UpdateFireVisuals();

            TakeDamage(fireDamagePerSecond * Time.deltaTime);

            if (!burnUntilDeath && fireTimer <= 0f)
                ExtinguishFire();
        }

        if (IsNetworked && IsServer)
        {
            witherSyncTimer -= Time.deltaTime;
            if (witherSyncTimer <= 0f)
            {
                witherSyncTimer = 0.25f;
                netWither.Value = Mathf.Clamp01(timer / witheringTime);
            }
        }

        if (timer <= 0 || health <= 0)
            Die();
    }

    private void UpdateFireVisuals()
    {
        if (!spriteRenderer || !isOnFire) return;
        float time = Time.time * fireFlickerSpeed;
        float cycle = time % 3f;

        Color fireColor;

        if (cycle < 1f)
        {
            fireColor = Color.Lerp(originalColor, Color.red, cycle);
        }
        else if (cycle < 2f)
        {
            float t = cycle - 1f;
            fireColor = Color.Lerp(Color.red, Color.yellow, t);
        }
        else
        {
            float t = cycle - 2f;
            fireColor = Color.Lerp(Color.yellow, originalColor, t);
        }

        spriteRenderer.color = fireColor;
    }

    private void Die()
    {
        if (isDying) return;
        isDying = true;

        if (IsNetworked)
        {
            // Server clears the tile everywhere and despawns this object.
            GameSession.Instance?.ServerDespawnPlant(this);
            return;
        }

        if (FarmManager.instance != null)
        {
            FarmManager.instance.NotifyPlantDeath(cellPos);
        }

        Destroy(gameObject);
    }

    protected bool IsFullyGrown() => stage == GrowthStage.Mature;

    // ======================================================
    //  INTERACTIONS (authority only — routed via GameSession online)
    // ======================================================
    public void Interact()
    {
        if (!IsSimAuthority) return;
        if (IsFullyGrown()) return;

        currentInteractions++;

        if (currentInteractions >= interactionsToMature)
            SetStage(GrowthStage.Mature);
        else if (stage == GrowthStage.Seed)
            SetStage(GrowthStage.Growing);
    }

    public void WaterPlant()
    {
        if (!IsSimAuthority) return;

        if (isOnFire)
        {
            ExtinguishFire();
        }
        timer = witheringTime;

        if (stage == GrowthStage.Mature) return;

        currentInteractions++;

        if (currentInteractions >= interactionsToMature)
            SetStage(GrowthStage.Mature);
        else if (stage == GrowthStage.Seed)
            SetStage(GrowthStage.Growing);
    }

    public void FertilizePlant()
    {
        if (!IsSimAuthority) return;

        if (stage == GrowthStage.Mature)
        {
            return;
        }

        currentInteractions = interactionsToMature;
        SetStage(GrowthStage.Mature);
        timer = witheringTime;
    }

    public virtual void TakeDamage(float damage)
    {
        if (!IsSimAuthority) return;
        health -= damage;
    }

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

    public float GetWitherRatio() =>
        IsNetworked && !IsServer
            ? Mathf.Clamp01(netWither.Value)
            : Mathf.Clamp01(timer / witheringTime);

    // ======================================================
    //  FIRE
    // ======================================================
    public void SetOnFire()
    {
        if (!IsSimAuthority) return;
        if (isOnFire) return;

        isOnFire = true;
        fireTimer = fireDuration;
        CaptureOriginalColor();

        if (IsNetworked && IsServer)
            netFire.Value = true;
    }

    public void ExtinguishFire()
    {
        if (!IsSimAuthority) return;

        isOnFire = false;
        fireTimer = 0f;

        if (spriteRenderer && health > 0)
            spriteRenderer.color = originalColor;

        if (IsNetworked && IsServer)
            netFire.Value = false;
    }

    private void OnFireChanged(bool oldValue, bool newValue)
    {
        if (IsServer) return; // the server already applied its own visuals

        isOnFire = newValue;

        if (newValue)
        {
            CaptureOriginalColor();
        }
        else if (spriteRenderer)
        {
            spriteRenderer.color = originalColor;
        }
    }

    private void CaptureOriginalColor()
    {
        if (spriteRenderer)
        {
            if (originalColor == Color.clear || originalColor == default(Color))
                originalColor = spriteRenderer.color;
        }
    }
}
