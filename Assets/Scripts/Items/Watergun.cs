using UnityEngine;
using Mirror;

public class Watergun : NetworkBehaviour
{
    [Range(0.1f, 100f)]
    [SerializeField] private float speed = 10f;

    [Range(0.1f, 10f)]
    [SerializeField] private float lifetime = 0.3f;

    [Range(0f, 45f)]
    [SerializeField] private float spreadAngle = 15f;

    [Header("Player Push Settings")]
    [Range(1f, 50f)]
    [SerializeField] private float pushForce = 25f;

    [Range(0.1f, 2f)]
    [SerializeField] private float pushDuration = 0.5f;
    [SerializeField] private Pickup pickup;

    private Rigidbody2D rb;

    // server->client inherited velocity
    [SyncVar(hook = nameof(OnInheritedVelocityChanged))]
    private Vector2 inheritedVelocitySync = Vector2.zero;
    private Vector2 inheritedVelocityLocal = Vector2.zero;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifetime);

        // OnStartServer/OnStartClient never fire offline (never Mirror-spawned there),
        // so play the VFX directly in that case.
        bool isNetworkSpawnedObject = isServer || isClient;
        if (!isNetworkSpawnedObject)
            PlayAllParticleSystems();
    }

    // Runs directly offline; server-authoritative online (see isNetworkSpawnedObject guard).
    public void SetInheritedVelocityServer(Vector2 velocity)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        inheritedVelocitySync = velocity;
        inheritedVelocityLocal = velocity;
    }

    void OnInheritedVelocityChanged(Vector2 oldVal, Vector2 newVal)
    {
        inheritedVelocityLocal = newVal;
    }

    // Ensure particle systems play on clients when spawned
    public override void OnStartClient()
    {
        base.OnStartClient();
        PlayAllParticleSystems();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        PlayAllParticleSystems();
    }

    private void PlayAllParticleSystems()
    {
        var systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
            ps.Play();
    }

    private void FixedUpdate()
    {
        float randomSpread = Random.Range(-spreadAngle, spreadAngle);
        Vector2 direction = Quaternion.Euler(0, 0, randomSpread) * transform.up;
        rb = rb ?? GetComponent<Rigidbody2D>();
        rb.linearVelocity = (direction * speed) + inheritedVelocityLocal;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Plant plant = other.gameObject.GetComponent<Plant>();
        if (plant != null)
        {
            plant.ExtinguishFire();
            plant.WaterPlant();
        }

        Player player = other.gameObject.GetComponent<Player>();
        if (player != null)
        {
            PushPlayer(player);
        }
    }

    private void PushPlayer(Player player)
    {
        Vector2 pushDirection = (player.transform.position - transform.position).normalized;
        player.ForceIdleState();
        player.ApplyPushForce(pushDirection, pushForce);
    }
}
