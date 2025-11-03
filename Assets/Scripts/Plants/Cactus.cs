using System.Collections;
using UnityEngine;

public class Cactus : Plant
{
    [Header("Water Absorption")]
    [SerializeField] private float maxWaterStorage = 50f;
    [SerializeField][Range(0.1f, 1.5f)] private float waterConsumptionRate = 0.5f;
    private float currentWaterStorage;

    protected override void Awake()
    {
        base.Awake();
        currentWaterStorage = maxWaterStorage; // start full
    }

    protected override void Update()
    {
        base.Update();

        if (currentWaterStorage > 0 && IsFullyGrown())
            DrinkWater();
    }

    private void DrinkWater()
    {
        timer = witheringTime;
        currentWaterStorage -= waterConsumptionRate;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject == this.gameObject || !IsFullyGrown()) return;

        Player otherPlayer = collision.gameObject.GetComponent<Player>();
        if (otherPlayer != null && otherPlayer.input.playerIndex != ownerPlayerIndex)
        {
            // Steal water
            float waterToSteal = Mathf.Min(waterConsumptionRate, otherPlayer.waterSupply);
            otherPlayer.waterSupply -= waterToSteal;
            currentWaterStorage = Mathf.Min(currentWaterStorage + waterToSteal, maxWaterStorage);
        }
    }

    // Override fire damage: water absorbs damage first
    public override void TakeDamage(float damage)
    {
        if (currentWaterStorage > 0f)
        {
            float waterAbsorb = Mathf.Min(currentWaterStorage, damage);
            currentWaterStorage -= waterAbsorb;
            damage -= waterAbsorb;

            // Optional: visual feedback for water dissipating
            Debug.Log($"{gameObject.name} absorbed {waterAbsorb:F1} damage with water. Remaining water: {currentWaterStorage:F1}");
        }

        // If damage remains, apply to health
        if (damage > 0f)
        {
            base.TakeDamage(damage);
        }
    }
}
