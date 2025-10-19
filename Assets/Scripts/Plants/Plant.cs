using UnityEngine;

//Para la entrega final habra que hacer la clase planta ser heredado desde Entity
public class Plant : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    [Range(0, 5)][SerializeField] private int score = 1;
    [Range(0, 20)][SerializeField] private float maxHealth = 10;
    [Range(0f, 60f)][SerializeField] private float witheringTime = 30f;

    private float health;
    private float timer;

    public void Init(Sprite sprite)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }
    private void Awake()
    {
        health = maxHealth;
        timer = witheringTime;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0 || health <= 0)
            Die();
    }
    private void Die()
    {
        Destroy(gameObject);
    }

    public void WaterPlant()
    {
        timer = witheringTime;
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
    }
}
