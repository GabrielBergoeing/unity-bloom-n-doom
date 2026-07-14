using System.Collections;
using UnityEngine;

public class Cactus : Plant
{
    [Header("Water Absorption")]
    [SerializeField] private float maxWaterStorage = 50f;
    [SerializeField][Range(0.001f, 0.01f)] private float waterConsumptionRate = 0.005f;
    [SerializeField][Range(5, 15)] private int waterStealRate = 5;
    public float currentWaterStorage;

    protected override void Awake()
    {
        base.Awake();
        currentWaterStorage = maxWaterStorage; // start full
    }

    protected override void Update()
    {
        base.Update();

        if (!IsSimAuthority) return; // only the server simulates water storage online

        if (currentWaterStorage > 0 && IsFullyGrown())
            DrinkWater();
    }

    private void DrinkWater()
    {
        timer = witheringTime;
        currentWaterStorage -= waterConsumptionRate;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject == this.gameObject || !IsFullyGrown()) return;
        if (!IsSimAuthority) return; // server decides who loses water

        Player otherPlayer = collision.gameObject.GetComponent<Player>();
        if (otherPlayer != null && otherPlayer.PlayerIndex != ownerPlayerIndex)
        {
            // Steal water (otherPlayer.waterSupply is mirrored from its owner over the network)
            float waterToSteal = Mathf.Min(waterStealRate, otherPlayer.waterSupply);
            if (waterToSteal <= 0f) return;

            if (GameSession.OnlineActive && otherPlayer.net != null)
                otherPlayer.net.StealWaterClientRpc(waterToSteal); // the owner applies the loss
            else
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
