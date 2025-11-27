using UnityEngine;

public class Watergun : MonoBehaviour
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
    private Vector2 inheritedVelocity = Vector2.zero;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifetime);
    }

    public void SetInheritedVelocity(Vector2 velocity)
    {
        inheritedVelocity = velocity;
    }

    private void FixedUpdate()
    {
        float randomSpread = Random.Range(-spreadAngle, spreadAngle);
        Vector2 direction = Quaternion.Euler(0, 0, randomSpread) * transform.up;
        rb.linearVelocity = (direction * speed) + inheritedVelocity;
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
