using UnityEngine;
using Mirror;

public class Fire : NetworkBehaviour
{
    [Range(0.1f, 100f)]
    [SerializeField] private float speed = 10f;

    [Range(0.1f, 10f)]
    [SerializeField] private float lifetime = 0.3f;

    [Range(0f, 45f)]
    [SerializeField] private float spreadAngle = 15f;

    private Rigidbody2D rb;

    // Local copy used for physics simulation on both server and client.
    private Vector2 inheritedVelocityLocal = Vector2.zero;

    // Sync the inherited velocity so clients know the initial velocity.
    [SyncVar(hook = nameof(OnInheritedVelocityChanged))]
    private Vector2 inheritedVelocitySync = Vector2.zero;

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

    // Hook runs on clients (and server) when SyncVar changes.
    void OnInheritedVelocityChanged(Vector2 oldVal, Vector2 newVal)
    {
        inheritedVelocityLocal = newVal;
    }

    // Ensure particle systems play on clients when object spawns.
    public override void OnStartClient()
    {
        base.OnStartClient();
        PlayAllParticleSystems();
    }

    // Also play on server so host sees VFX immediately.
    public override void OnStartServer()
    {
        base.OnStartServer();
        PlayAllParticleSystems();
    }

    private void PlayAllParticleSystems()
    {
        var systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            // safe: Play will start system both in editor and builds
            ps.Play();
        }
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
            plant.SetOnFire();
        }
    }
}
