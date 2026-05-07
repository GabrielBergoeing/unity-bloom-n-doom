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
    private float currentAmmo;
    private float nextFireTime;

    private Player owner; // who currently holds it

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
                return;
            }
        }

        // Only the local player reads inputs and requests the server to shoot.
        if (owner.isLocalPlayer)
        {
            bool isFiring = owner.input.actions["Shoot"].ReadValue<float>() > 0f;

            if (isFiring && currentAmmo > 0f)
            {
                // request server to shoot (Command on Player)
                owner.CmdRequestShoot();
            }

            // local cooldown of ammo (visual/UX). Actual ammo consumption enforced on server.
            if (isFiring && currentAmmo > 0f)
            {
                currentAmmo -= Time.deltaTime;
                if (currentAmmo < 0f) currentAmmo = 0f;
            }
        }

        // On server the ServerShoot method enforces fireRate and ammo consumption.
    }

    // This method runs on the server to spawn projectiles.
    // Call from server-side (e.g. Player.CmdRequestShoot -> finds current item and calls this).
    [Server]
    public void ServerShoot(Vector2 ownerVelocity)
    {
        if (nextFireTime > Time.time) return;
        if (currentAmmo <= 0f) return;

        nextFireTime = Time.time + fireRate;
        currentAmmo -= fireRate;
        if (currentAmmo < 0f) currentAmmo = 0f;

        if (sfx != null)
        {
            sfx.PlayOnUse();
        }

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

            NetworkServer.Spawn(fireInstance);
            var nid = fireInstance.GetComponent<NetworkIdentity>()?.netId ?? 0;
            Debug.Log($"[Flamethrower] Spawned projectile '{firePrefab.name}' netId={nid} at {fireInstance.transform.position}");
        }
    }
}