using UnityEngine;
using Mirror;

public class Flamethrower : NetworkBehaviour
{
    [SerializeField] private Pickup pickup;
    [SerializeField] private GameObject firePrefab;
    [SerializeField] private Transform fireSpawnPoint;

    [Range(0.001f, 5f)]
    [SerializeField] private float fireRate = 0.2f;

    [Range(1, 5)]
    [SerializeField] private int projectilesPerShot = 3;

    [Range(0f, 360f)]
    [SerializeField] private float spreadAngle = 30f;

    [SerializeField] private float maxAmmoSeconds = 10f;

    // Server-authoritative; synced to all clients so the client-side ammo gate
    // (line below in Update) never drifts from what ServerShoot actually enforces.
    [SyncVar] private float currentAmmo;
    private float nextFireTime;

    private Player owner; // who currently holds it
    private bool loggedMissingOwner;

    public Items_SFX sfx { get; private set; }

    private void Awake()
    {
        sfx = GetComponent<Items_SFX>();
    }

    private void Start()
    {
        pickup = GetComponent<Pickup>();
        currentAmmo = maxAmmoSeconds;

        // server-side owner assignment (remains useful for server logic)
        pickup.OnPickup += (player) => owner = player;
        pickup.OnDrop += (_) => owner = null;
    }

    private void Update()
    {
        // Attempt to resolve owner locally if not set (useful for clients:
        // HotbarSystem parentea el item bajo "OnHand" transform).
        if (owner == null)
        {
            owner = GetComponentInParent<Player>();
            if (owner == null)
            {
                // not held by any player on this client -> nothing to do
                if (!loggedMissingOwner && transform.parent != null)
                {
                    loggedMissingOwner = true;
                    Debug.LogWarning($"[Flamethrower] '{name}' is parented under '{transform.parent.name}' but no Player component was found in its parents - shooting will never fire.");
                }
                return;
            }
            loggedMissingOwner = false;
            Debug.Log($"[Flamethrower] '{name}' resolved owner={owner.name} isLocalPlayer={owner.isLocalPlayer} isServer={owner.isServer} isClient={owner.isClient}");
        }

        // isLocalPlayer is never true offline (never Mirror-spawned), so gate on that
        // only when actually networked; offline just needs local input handling.
        bool isNetworkSpawnedObject = owner.isServer || owner.isClient;
        if (isNetworkSpawnedObject && !owner.isLocalPlayer) return;

        if (owner.input == null || !owner.input.enabled) return;

        bool isFiring = owner.input.actions["Shoot"].ReadValue<float>() > 0f;

        if (isFiring)
        {
            if (currentAmmo > 0f)
            {
                if (isNetworkSpawnedObject)
                    owner.CmdRequestShoot(); // request server to shoot (Command on Player)
                else
                    ServerShoot(owner.rb != null ? owner.rb.linearVelocity : Vector2.zero);
            }
            else
            {
                Debug.Log($"[Flamethrower] '{name}' fire pressed but currentAmmo={currentAmmo} - out of ammo.");
            }
        }

        // Ammo is consumed server-side in ServerShoot and replicated to all clients via
        // the [SyncVar] above, so it is never predicted/decremented locally here anymore.
    }

    // Runs directly offline; server-authoritative online (see isNetworkSpawnedObject guard).
    // Call from server-side (e.g. Player.CmdRequestShoot -> finds current item and calls this).
    public void ServerShoot(Vector2 ownerVelocity)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer)
        {
            Debug.LogWarning($"[Flamethrower][ServerShoot] '{name}' called on a non-server instance - ignoring.");
            return;
        }

        if (nextFireTime > Time.time)
        {
            Debug.Log($"[Flamethrower][ServerShoot] '{name}' rejected: fireRate cooldown ({nextFireTime - Time.time:F3}s left).");
            return;
        }

        if (currentAmmo <= 0f)
        {
            Debug.Log($"[Flamethrower][ServerShoot] '{name}' rejected: out of ammo on server (currentAmmo={currentAmmo}).");
            return;
        }

        nextFireTime = Time.time + fireRate;
        currentAmmo -= fireRate;
        if (currentAmmo < 0f) currentAmmo = 0f;

        if (isNetworkSpawnedObject)
            RpcPlayFireSound();
        else if (sfx != null)
            sfx.PlayOnUse();

        float angleStep = projectilesPerShot > 1 ? spreadAngle / (projectilesPerShot - 1) : 0f;
        float startAngle = -spreadAngle / 2;

        for (int i = 0; i < projectilesPerShot; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Quaternion rotation = fireSpawnPoint.rotation * Quaternion.Euler(0, 0, currentAngle);

            GameObject fireInstance = Instantiate(firePrefab, fireSpawnPoint.position, rotation);

            // Configure networked projectile before spawning
            var fireNet = fireInstance.GetComponent<Fire>();
            if (fireNet != null)
            {
                // Set the SyncVar so clients receive inherited velocity on spawn
                fireNet.SetInheritedVelocityServer(ownerVelocity);
            }

            if (isServer)
            {
                NetworkServer.Spawn(fireInstance);
                var nid = fireInstance.GetComponent<NetworkIdentity>()?.netId ?? 0;
                Debug.Log($"[Flamethrower] Spawned projectile '{firePrefab.name}' netId={nid} at {fireInstance.transform.position}");
            }
        }
    }

    // Server-only method (ServerShoot) plays it via this Rpc so remote clients (and the
    // dedicated server, which has no ears) all hear the shot, not just the host.
    [ClientRpc]
    private void RpcPlayFireSound()
    {
        if (sfx != null)
            sfx.PlayOnUse();
    }
}