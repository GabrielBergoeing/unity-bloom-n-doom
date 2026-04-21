using Mirror;
using UnityEngine;

public class Flamethrower : NetworkBehaviour
{
    [SerializeField] private GameObject firePrefab;          // Prefab de gameplay (debe tener NetworkIdentity para spawnear)
    [SerializeField] private GameObject fireVfxPrefab;       // Opcional: VFX solo visual (sin NetworkIdentity)
    [SerializeField] private Transform fireSpawnPoint;

    [Range(0.001f, 5f)]
    [SerializeField] private float fireRate = 0.2f;

    [Range(1, 5)]
    [SerializeField] private int projectilesPerShot = 3;

    [Range(0f, 360f)]
    [SerializeField] private float spreadAngle = 30f;

    private float nextFireTime;
    private Player owner;

    public Items_SFX sfx { get; private set; }

    private void Awake()
    {
        sfx = GetComponent<Items_SFX>();
    }

    private void Start()
    {
        var pickup = GetComponent<Pickup>();
        if (pickup != null)
        {
            pickup.OnPickup += (p) => owner = p;
            pickup.OnDrop += (_) => owner = null;
        }
    }

    [Server]
    public void ServerShoot()
    {
        if (Time.time < nextFireTime) return;

        // Seguridad: asegurarse de que tenemos lo necesario
        if (firePrefab == null)
        {
            Debug.LogWarning("[Flamethrower] firePrefab no asignado. Abortando disparo.", this);
            return;
        }
        if (fireSpawnPoint == null)
        {
            Debug.LogWarning("[Flamethrower] fireSpawnPoint no asignado. Abortando disparo.", this);
            return;
        }

        nextFireTime = Time.time + fireRate;
        Shoot();
    }

    [Server]
    private void Shoot()
    {
        if (owner == null) return;

        if (sfx != null)
            sfx.PlayOnUse();

        if (projectilesPerShot <= 0) projectilesPerShot = 1;

        float angleStep = (projectilesPerShot > 1) ? spreadAngle / (projectilesPerShot - 1) : 0f;
        float startAngle = -spreadAngle / 2f;

        for (int i = 0; i < projectilesPerShot; i++)
        {
            float angle = startAngle + angleStep * i;

            // rot calculada correctamente
            Quaternion rot = fireSpawnPoint.rotation * Quaternion.Euler(0f, 0f, angle);

            GameObject fire = Instantiate(firePrefab, fireSpawnPoint.position, rot);
            NetworkServer.Spawn(fire);

            var fireScript = fire.GetComponent<Fire>();
            if (fireScript != null && owner != null)
            {
                fireScript.SetInheritedVelocity(owner.rb != null ? owner.rb.linearVelocity : Vector2.zero);
            }
        }

        // Aviso visual a clientes (si hay prefab VFX no networked)
        if (fireVfxPrefab != null)
            RpcPlayVfx(fireSpawnPoint.position, fireSpawnPoint.rotation);
    }

    [ClientRpc]
    void RpcPlayVfx(Vector3 pos, Quaternion rot)
    {
        if (fireVfxPrefab == null) return;
        Instantiate(fireVfxPrefab, pos, rot);
    }
}