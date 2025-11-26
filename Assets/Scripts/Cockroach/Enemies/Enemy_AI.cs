using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public Node rootNode;
    public LevelManager levelManager; // Provides day/night info
    public float velocity = 2f;
    public float direction;
    private float currentTime;
    
    [Header("Collision Detection")]
    public LayerMask wallLayerMask = -1;
    public float raycastDistance = 0.5f; 

    private void Start()
    {
        // Find LevelManager in the scene if not assigned
        if (levelManager == null)
            levelManager = FindFirstObjectByType<LevelManager>();
    }
    
    private void Update()
    {
        if (rootNode == null) return;

        // Create a simple world context
        var worldContext = new WorldContext
        {
            player = GameObject.FindWithTag("Player"),
            alertRange = 5f
        };

        rootNode.Decide(gameObject, worldContext);
    }

    public void ExecuteAction(string action)
    {
        switch (action)
        {
            case "Wander":
                Wander();
                break;
            case "Flee":
                Flee();
                break;
            case "Chase":
                Chase();
                break;
        }
    }

    private void Wander()
    {
        currentTime += Time.deltaTime;
        Vector3 wanderDir = new Vector3(Mathf.Cos(direction), Mathf.Sin(direction), 0);
        
        if (CanMove(wanderDir))
        {
            transform.position += wanderDir * velocity * Time.deltaTime;
        }
        else
        {
            direction = Random.Range(0.0f, Mathf.PI * 2f);
            currentTime = 0f;
        }

        if (currentTime >= 2.0f)
        {
            direction = Random.Range(0.0f, Mathf.PI * 2f);
            currentTime = 0f;
        }
    }

    private void Flee()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Vector3 fleeDir = (transform.position - player.transform.position).normalized;
        if (CanMove(fleeDir))
        {
            transform.position += fleeDir * velocity * Time.deltaTime;
        }
        else
        {
            Vector3 alternativeDir = GetAlternativeDirection(fleeDir);
            if (CanMove(alternativeDir))
            {
                transform.position += alternativeDir * velocity * Time.deltaTime;
            }
        }
    }

    private void Chase()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Vector3 dir = (player.transform.position - transform.position).normalized;
        
        if (CanMove(dir))
        {
            transform.position += dir * velocity * Time.deltaTime;
        }
        else
        {
            Vector3 alternativeDir = GetAlternativeDirection(dir);
            if (CanMove(alternativeDir))
            {
                transform.position += alternativeDir * velocity * Time.deltaTime;
            }
        }
    }

    private bool CanMove(Vector3 direction)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, raycastDistance, wallLayerMask);
        return hit.collider == null;
    }

    private Vector3 GetAlternativeDirection(Vector3 blockedDirection)
    {

        float angle = Mathf.Atan2(blockedDirection.y, blockedDirection.x) + Mathf.PI / 4;
        Vector3 rightDir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
        
        if (CanMove(rightDir))
            return rightDir;
        
        angle = Mathf.Atan2(blockedDirection.y, blockedDirection.x) - Mathf.PI / 4;
        Vector3 leftDir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
        
        if (CanMove(leftDir))
            return leftDir;
        
        return new Vector3(-blockedDirection.y, blockedDirection.x, 0);
    }
}
