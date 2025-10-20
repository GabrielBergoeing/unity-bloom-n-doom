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

    private float nextFireTime;

    private void Start()
    {
        pickup = GetComponent<Pickup>();
    }
    private void Update()
    {
        if (!pickup.isPickedUp) return;
        if (Input.GetMouseButton(0) && nextFireTime <= 0f)
            {
                Shoot();
                nextFireTime = fireRate;
            }
            else
            {
                nextFireTime -= Time.deltaTime;
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
