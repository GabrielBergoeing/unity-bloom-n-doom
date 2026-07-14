using UnityEngine;

public class Flamethrower : MonoBehaviour
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

    // Who currently wields it: set through Pickup.holder (hand instances) or pickup events (offline).
    private Player owner => pickup != null ? pickup.holder : null;

    public Items_SFX sfx { get; private set; }

    private void Awake()
    {
        sfx = GetComponent<Items_SFX>();
        pickup = GetComponent<Pickup>();
    }

    private void Start()
    {
        currentAmmo = maxAmmoSeconds;
    }

    private void Update()
    {
        if (owner == null) return;        // not held by a player
        if (!owner.IsLocalOwner) return;  // remote hand visuals never have a holder, but be safe

        bool isFiring = owner.input.actions["Shoot"].ReadValue<float>() > 0f;

        if (isFiring && currentAmmo > 0f)
        {
            currentAmmo -= Time.deltaTime;
            if (currentAmmo < 0f) currentAmmo = 0f;

            if (nextFireTime <= 0f)
            {
                Shoot();
                nextFireTime = fireRate;
            }
            else
            {
                nextFireTime -= Time.deltaTime;
            }
        }
        else
        {
            nextFireTime -= Time.deltaTime;
        }

        // Out of fuel — drop
        if (currentAmmo <= 0f)
        {
            pickup.Consume(owner); 
        }
    }

    private void Shoot()
    {
        if (sfx != null)
        {
            sfx.PlayOnUse();
        }
        

        float angleStep = spreadAngle / (projectilesPerShot - 1);
        float startAngle = -spreadAngle / 2;

        int projectileId = GameSession.OnlineActive && NetworkAssets.Instance != null
            ? NetworkAssets.Instance.ProjectileIdOf(firePrefab)
            : -1;

        for (int i = 0; i < projectilesPerShot; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Quaternion rotation =
                fireSpawnPoint.rotation * Quaternion.Euler(0, 0, currentAngle);
            Vector2 ownerVelocity = Vector2.zero;
            if (owner != null)
            {
                ownerVelocity = owner.rb.linearVelocity;
            }

            if (GameSession.OnlineActive)
            {
                // Server simulates the authoritative projectile; every peer spawns a visual copy.
                if (projectileId >= 0)
                    GameSession.Instance?.RequestProjectileServerRpc(
                        projectileId, fireSpawnPoint.position, rotation, ownerVelocity);
                continue;
            }

            GameObject fireInstance = Instantiate(firePrefab, fireSpawnPoint.position, rotation);

            //Esto es feito pero funciona
            Fire fireScript = fireInstance.GetComponent<Fire>();
            if (fireScript != null)
            {
                fireScript.SetInheritedVelocity(ownerVelocity);
            }
            Watergun watergunScript = fireInstance.GetComponent<Watergun>();
            if (watergunScript != null)
            {
                watergunScript.SetInheritedVelocity(ownerVelocity);
            }
        }
    }
}
