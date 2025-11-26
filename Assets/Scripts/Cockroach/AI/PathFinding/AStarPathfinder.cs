using System.Collections.Generic;
using UnityEngine;

public interface IPathfinder
{
    List<Vector3> FindPath(Vector3 startWorld, Vector3 goalWorld);
}

public class AStarPathfinder : MonoBehaviour, IPathfinder
{
    [Header("Referencias")]
    public GridGraph graph;

    private readonly List<Vector3> emptyPath = new();

    private void Awake()
    {
        if (graph == null) graph = GetComponent<GridGraph>();
    }

    public List<Vector3> FindPath(Vector3 startWorld, Vector3 goalWorld)
    {
        if (graph == null) return emptyPath;

        GraphNode start = graph.GetNodeFromWorld(startWorld);
        GraphNode goal  = graph.GetNodeFromWorld(goalWorld);
        if (start == null || goal == null || !start.Walkable || !goal.Walkable) return emptyPath;

        // Reset A*
        for (int x = 0; x < graph.width; x++)
        for (int y = 0; y < graph.height; y++)
        {
            var n = graph.GetNode(x, y);
            if (n == null) continue;
            n.GValue = float.PositiveInfinity;
            n.HValue = 0f;
            n.CameFrom = null;
        }

        var open   = new SimplePriorityQueue<GraphNode>();
        var inOpen = new HashSet<GraphNode>();
        var closed = new HashSet<GraphNode>();

        start.GValue = 0f;
        start.HValue = Heuristic(start, goal); 
        open.Enqueue(start, start.F());
        inOpen.Add(start);

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            inOpen.Remove(current);

            if (current == goal)
                return ReconstructPath(goal);

            closed.Add(current);

            foreach (var neighbor in graph.GetNeighbors(current))
            {
                if (closed.Contains(neighbor)) continue;

                float stepCost = Vector2.Distance(current.WorldPos, neighbor.WorldPos);
                float tentativeG = current.GValue + stepCost;

                if (tentativeG < neighbor.GValue)
                {
                    neighbor.CameFrom = current;
                    neighbor.GValue   = tentativeG;
                    neighbor.HValue   = Heuristic(neighbor, goal);

                    float f = neighbor.F();
                    if (!inOpen.Contains(neighbor))
                    {
                        open.Enqueue(neighbor, f);
                        inOpen.Add(neighbor);
                    }
                    else
                    {
                        open.UpdatePriority(neighbor, f);
                    }
                }
            }
        }

        return emptyPath;
    }

    private float Heuristic(GraphNode a, GraphNode b)
    {
        return Mathf.Abs(a.WorldPos.x - b.WorldPos.x) + Mathf.Abs(a.WorldPos.y - b.WorldPos.y);
    }

    private List<Vector3> ReconstructPath(GraphNode goal)
    {
        var path = new List<Vector3>();
        var curr = goal;
        while (curr != null)
        {
            path.Add(curr.WorldPos);
            curr = curr.CameFrom;
        }
        path.Reverse();
        return path;
    }

    private class SimplePriorityQueue<T>
    {
        private readonly List<(T item, float priority)> data = new();
        public int Count => data.Count;

        public void Enqueue(T item, float priority) => data.Add((item, priority));

        public T Dequeue()
        {
            int best = 0;
            float bestP = data[0].priority;
            for (int i = 1; i < data.Count; i++)
                if (data[i].priority < bestP) { best = i; bestP = data[i].priority; }
            var item = data[best].item;
            data.RemoveAt(best);
            return item;
        }

        public void UpdatePriority(T item, float newPriority)
        {
            for (int i = 0; i < data.Count; i++)
                if (EqualityComparer<T>.Default.Equals(data[i].item, item))
                { data[i] = (item, newPriority); return; }
            Enqueue(item, newPriority);
        }
    }
}
