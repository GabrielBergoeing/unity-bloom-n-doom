using UnityEngine;
using UnityEngine.InputSystem;

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

    private float nextFireTime;
    [SerializeField] private float maxAmmoSeconds = 10f;
    private float currentAmmo = 0f;

    private void Start()
    {
        pickup = GetComponent<Pickup>();
        currentAmmo = maxAmmoSeconds;
    }
    private void Update()
    {
        if (!pickup.isPickedUp) return;

        bool firing = pickup.playerInput.actions["Sabotage"].ReadValue<float>() > 0f;

        if (firing && currentAmmo > 0f)
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

        if (currentAmmo <= 0f)
        {
            pickup.DropItem(false, true);
            currentAmmo = maxAmmoSeconds;
        }
    }

    private void Shoot()
    {
        float angleStep = spreadAngle / (projectilesPerShot - 1);
        float startAngle = -spreadAngle / 2;

        for (int i = 0; i < projectilesPerShot; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Quaternion rotation = fireSpawnPoint.rotation * Quaternion.Euler(0, 0, currentAngle);
            Instantiate(firePrefab, fireSpawnPoint.position, rotation);
        }
    }

}
