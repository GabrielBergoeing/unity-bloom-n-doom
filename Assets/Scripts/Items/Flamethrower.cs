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

        pickup.OnPickup += (player) => owner = player;
        pickup.OnDrop += (_) => owner = null;
    }

    private void Update()
    {
        if (owner == null) return; // not held by player

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
        sfx.PlayOnUse();

        float angleStep = spreadAngle / (projectilesPerShot - 1);
        float startAngle = -spreadAngle / 2;

        for (int i = 0; i < projectilesPerShot; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Quaternion rotation =
                fireSpawnPoint.rotation * Quaternion.Euler(0, 0, currentAngle);

            Instantiate(firePrefab, fireSpawnPoint.position, rotation);
        }
    }
}
