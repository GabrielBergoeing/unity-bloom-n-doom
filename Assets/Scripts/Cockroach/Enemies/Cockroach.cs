using System.Collections.Generic;
using UnityEngine;

public class Cockroach : MonoBehaviour
{
    [Header("Decision Tree")]
    public Node rootNode;
    
    [Header("Movement")]
    public float velocity = 1.5f;
    public float rotationSpeed = 5f;
    
    [Header("Detection")]
    public float plantDetectionRange = 3f;
    public float playerDetectionRange = 4f;
    public LayerMask plantLayerMask = -1;
    public LayerMask wallLayerMask = -1;
    
    [Header("Pathfinding")]
    public AStarPathfinder pathfinder;
    public float pathUpdateInterval = 0.5f;
    public float waypointTolerance = 0.2f;
    
    [Header("Plant Damage")]
    public float damageAmount = 1f;
    public float damageInterval = 5f;
    public float damageRange = 0.3f;
    
    [Header("Death System")]
    public GameObject deathEffect; // Opcional: efecto de partículas al morir
    public AudioClip deathSound;   // Opcional: sonido al morir
    
    [Header("Wander Behavior")]
    public float wanderRadius = 5f;
    public float wanderTime = 3f;
    

    private List<Vector3> currentPath = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private float lastPathUpdate = 0f;
    private Vector3 wanderTarget;
    private float wanderTimer = 0f;
    private float lastDamageTime = 0f;
    private CockroachWorldContext worldContext;
    private GameObject targetPlant;
    private bool isDead = false;
    
    private void Start()
    {
        if (pathfinder == null)
            pathfinder = GetComponent<AStarPathfinder>();
        
        worldContext = new CockroachWorldContext();
        UpdateWorldContext();
        SetRandomWanderTarget();
    }
    
    private void Update()
    {
        // Si está muerta, no hacer nada
        if (isDead) return;
        
        UpdateWorldContext();
        
        if (rootNode != null)
        {
            rootNode.Decide(gameObject, worldContext);
        }
    }
    
    private void UpdateWorldContext()
    {

        GameObject player = GameObject.FindWithTag("Player");
        worldContext.player = player;
        
        if (player != null)
        {
            worldContext.distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            worldContext.isPlayerNear = worldContext.distanceToPlayer <= playerDetectionRange;
        }
        else
        {
            worldContext.distanceToPlayer = float.MaxValue;
            worldContext.isPlayerNear = false;
        }
        
        GameObject nearestPlant = FindNearestPlant();
        worldContext.nearestPlant = nearestPlant;
        
        if (nearestPlant != null)
        {
            worldContext.distanceToPlant = Vector3.Distance(transform.position, nearestPlant.transform.position);
            worldContext.isPlantVisible = worldContext.distanceToPlant <= plantDetectionRange;
            worldContext.isPlantInDamageRange = worldContext.distanceToPlant <= damageRange;
        }
        else
        {
            worldContext.distanceToPlant = float.MaxValue;
            worldContext.isPlantVisible = false;
            worldContext.isPlantInDamageRange = false;
        }
    }
    
    private GameObject FindNearestPlant()
    {
        Collider2D[] plants = Physics2D.OverlapCircleAll(transform.position, plantDetectionRange, plantLayerMask);
        
        GameObject nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var plant in plants)
        {
            float distance = Vector3.Distance(transform.position, plant.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = plant.gameObject;
            }
        }
        
        return nearest;
    }
    
    public void ExecuteAction(string action)
    {
        switch (action)
        {
            case "SearchForPlants":
                SearchForPlants();
                break;
            case "GoToPlant":
                GoToPlant();
                break;
            case "FleeFromPlayer":
                FleeFromPlayer();
                break;
        }
    }
    
    private void SearchForPlants()
    {
        wanderTimer += Time.deltaTime;
        
        if (wanderTimer >= wanderTime || Vector3.Distance(transform.position, wanderTarget) < waypointTolerance)
        {
            SetRandomWanderTarget();
        }
        
        MoveToTarget(wanderTarget);
    }
    
    private void SetRandomWanderTarget()
    {
        Vector3 randomDirection = Random.insideUnitCircle.normalized * wanderRadius;
        wanderTarget = transform.position + randomDirection;
        wanderTimer = 0f;
        
        // Clear current path to force recalculation
        currentPath.Clear();
        currentWaypointIndex = 0;
    }
    
    private void GoToPlant()
    {
        if (worldContext.nearestPlant != null)
        {
            targetPlant = worldContext.nearestPlant;
            
            if (worldContext.isPlantInDamageRange)
            {
                DamagePlant(targetPlant);
            }
            
            if (!worldContext.isPlantInDamageRange)
            {
                MoveToTarget(targetPlant.transform.position);
            }
        }
        else
        {
            SearchForPlants();
        }
    }
    
    private void FleeFromPlayer()
    {
        if (worldContext.player != null)
        {
            Vector3 fleeDirection = (transform.position - worldContext.player.transform.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDirection * playerDetectionRange;
            
            MoveToTarget(fleeTarget);
        }
    }
    
    private void MoveToTarget(Vector3 target)
    {
        // Update path if needed
        if (Time.time - lastPathUpdate > pathUpdateInterval || currentPath.Count == 0)
        {
            UpdatePathTo(target);
        }
        
        // Follow current path
        if (currentPath.Count > 0 && currentWaypointIndex < currentPath.Count)
        {
            Vector3 currentWaypoint = currentPath[currentWaypointIndex];
            Vector3 direction = (currentWaypoint - transform.position).normalized;
            
            // Check if we reached the current waypoint
            if (Vector3.Distance(transform.position, currentWaypoint) < waypointTolerance)
            {
                currentWaypointIndex++;
            }
            
            // Move towards waypoint
            if (currentWaypointIndex < currentPath.Count)
            {
                transform.position += direction * velocity * Time.deltaTime;
                
                // Rotate towards movement direction
                if (direction != Vector3.zero)
                {
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }
    }
    
    private void UpdatePathTo(Vector3 target)
    {
        if (pathfinder != null)
        {
            currentPath = pathfinder.FindPath(transform.position, target);
            currentWaypointIndex = 0;
            lastPathUpdate = Time.time;
        }
    }
    
    private void DamagePlant(GameObject plantObject)
    {
        // Verificar que haya pasado suficiente tiempo desde el último daño
        if (Time.time - lastDamageTime < damageInterval) return;
        
        // Intentar obtener el componente Plant
        Plant plant = plantObject.GetComponent<Plant>();
        if (plant != null)
        {
            // Hacer daño a la planta
            plant.TakeDamage(damageAmount);
            lastDamageTime = Time.time;
            
            // Debug para ver que está funcionando
            Debug.Log($"Cockroach dealing {damageAmount} damage to {plant.name}");
        }
        else
        {
            // Si no tiene componente Plant, intentar con otros componentes
            // Puedes agregar aquí otros tipos de plantas o componentes de salud
            Debug.LogWarning($"Plant {plantObject.name} doesn't have Plant component!");
        }
    }
    
    // Métodos para detectar cuando el jugador pisa la cucaracha
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Verificar si el que entró es el jugador
        if (other.CompareTag("Player") && !isDead)
        {
            Die();
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Verificar si el que colisionó es el jugador
        if (collision.gameObject.CompareTag("Player") && !isDead)
        {
            Die();
        }
    }
    
    private void Die()
    {
        if (isDead) return; // Evitar morir dos veces
        
        isDead = true;
        
        // Efectos de muerte
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, transform.rotation);
        }
        
        // Sonido de muerte
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }
        
        // Debug para confirmar que murió
        Debug.Log("Cockroach has been squished!");
        
        // Destruir la cucaracha después de un pequeño delay para que se vean los efectos
        Destroy(gameObject, 0.1f);
    }
    
    // Gizmos for debugging
    private void OnDrawGizmosSelected()
    {
        // Draw detection ranges
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, plantDetectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, playerDetectionRange);
        
        // Draw damage range
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f); // Color naranja
        Gizmos.DrawWireSphere(transform.position, damageRange);
        
        // Draw current path
        if (currentPath.Count > 1)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }
        }
        
        // Draw wander target
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(wanderTarget, 0.2f);
    }
}

[System.Serializable]
public class CockroachWorldContext
{
    public GameObject player;
    public GameObject nearestPlant;
    public float distanceToPlayer;
    public float distanceToPlant;
    public bool isPlayerNear;
    public bool isPlantVisible;
    public bool isPlantInDamageRange;
}