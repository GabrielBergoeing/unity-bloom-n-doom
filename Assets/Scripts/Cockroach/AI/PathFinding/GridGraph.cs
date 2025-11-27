using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class GridGraph : MonoBehaviour
{
    [Header("Tilemaps de obstáculos")]
    public Tilemap[] obstacleTilemaps;
    
    [Header("Auto-búsqueda")]
    [SerializeField] private string[] tilemapNamesToFind = { "Walls", "Wall", "Water", "Agua", "Obstacles", "Obstaculos" };

    public int width  { get; private set; }
    public int height { get; private set; }
    public float cellSize { get; private set; }
    public Vector3 originWorld { get; private set; }

    private GraphNode[,] nodes;
    private BoundsInt bounds;
    private bool built = false;

    private void Awake()      => TryBuild();
    private void OnEnable()   => TryBuild();
    private void OnValidate() => TryBuild(); 

    private void TryBuild()
    {
        if (!isActiveAndEnabled) return;

        AutoFindTilemaps();
        
        if (obstacleTilemaps == null || obstacleTilemaps.Length == 0) { built = false; return; }
        
        bool hasValidTilemap = false;
        foreach (var tilemap in obstacleTilemaps)
        {
            if (tilemap != null)
            {
                hasValidTilemap = true;
                break;
            }
        }
        
        if (!hasValidTilemap) { built = false; return; }
        Build();
    }
    
    public void Build()
    {
        Tilemap referenceTilemap = null;
        foreach (var tilemap in obstacleTilemaps)
        {
            if (tilemap != null)
            {
                referenceTilemap = tilemap;
                break;
            }
        }
        
        if (referenceTilemap == null) return;
        
        bounds = referenceTilemap.cellBounds;
        foreach (var tilemap in obstacleTilemaps)
        {
            if (tilemap != null && tilemap != referenceTilemap)
            {
                BoundsInt otherBounds = tilemap.cellBounds;
                bounds.xMin = Mathf.Min(bounds.xMin, otherBounds.xMin);
                bounds.yMin = Mathf.Min(bounds.yMin, otherBounds.yMin);
                bounds.xMax = Mathf.Max(bounds.xMax, otherBounds.xMax);
                bounds.yMax = Mathf.Max(bounds.yMax, otherBounds.yMax);
            }
        }
        
        width  = Mathf.Max(0, bounds.size.x);
        height = Mathf.Max(0, bounds.size.y);

        if (width == 0 || height == 0)
        {
            nodes = null;
            built = false;
            return;
        }

        cellSize    = referenceTilemap.layoutGrid.cellSize.x;
        originWorld = referenceTilemap.CellToWorld(bounds.min);

        nodes = new GraphNode[width, height];

        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                var cell = new Vector3Int(bounds.xMin + dx, bounds.yMin + dy, 0);
                
                bool hasObstacle = false;
                foreach (var tilemap in obstacleTilemaps)
                {
                    if (tilemap != null && tilemap.HasTile(cell))
                    {
                        hasObstacle = true;
                        break;
                    }
                }
                
                bool walkable = !hasObstacle;
                Vector3 worldPos = referenceTilemap.GetCellCenterWorld(cell);
                nodes[dx, dy] = new GraphNode(dx, dy, worldPos, walkable);
            }
        }

        built = true;
    }

    private void AutoFindTilemaps()
    {
        List<Tilemap> foundTilemaps = new List<Tilemap>();
        
        Tilemap[] allTilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        
        foreach (var tilemap in allTilemaps)
        {
            string tilemapName = tilemap.gameObject.name.ToLowerInvariant();
            
            foreach (string nameToFind in tilemapNamesToFind)
            {
                if (tilemapName.Contains(nameToFind.ToLowerInvariant()))
                {
                    foundTilemaps.Add(tilemap);
                    break;
                }
            }
        }
        if (foundTilemaps.Count > 0)
        {
            obstacleTilemaps = foundTilemaps.ToArray();
        }
    }

    public GraphNode GetNode(int x, int y)
    {
        if (nodes == null) return null;
        if (x < 0 || y < 0 || x >= width || y >= height) return null;
        return nodes[x, y];
    }

    public GraphNode GetNodeFromWorld(Vector3 worldPos)
    {
        if (nodes == null) return null;
    
        Tilemap referenceTilemap = null;
        foreach (var tilemap in obstacleTilemaps)
        {
            if (tilemap != null)
            {
                referenceTilemap = tilemap;
                break;
            }
        }
        
        if (referenceTilemap == null) return null;
        
        Vector3Int cell = referenceTilemap.WorldToCell(worldPos);
        int dx = cell.x - bounds.xMin;
        int dy = cell.y - bounds.yMin;
        return GetNode(dx, dy);
    }
    public IEnumerable<GraphNode> GetNeighbors(GraphNode node)
    {
        if (nodes == null || node == null) yield break;

        // 4-neighborhood
        int[,] d4 = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        for (int i = 0; i < d4.GetLength(0); i++)
        {
            int nx = node.X + d4[i, 0];
            int ny = node.Y + d4[i, 1];
            var n = GetNode(nx, ny);
            if (n != null && n.Walkable)
                yield return n;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!built || nodes == null) return;

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            var n = nodes[x, y];
            Gizmos.color = n.Walkable ? new Color(0f, 1f, 0f, 0.12f) : new Color(1f, 0f, 0f, 0.35f);
            Gizmos.DrawCube(n.WorldPos, Vector3.one * (cellSize * 0.92f));
        }
    }
}
