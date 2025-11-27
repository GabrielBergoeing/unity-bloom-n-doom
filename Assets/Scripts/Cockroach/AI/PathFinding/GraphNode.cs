using UnityEngine;

public class GraphNode
{
    public int X;
    public int Y;
    public Vector3 WorldPos;
    public bool Walkable;
    public float GValue;
    public float HValue;
    public GraphNode CameFrom;

    public GraphNode(int x, int y, Vector3 worldPos, bool walkable)
    {
        X = x; Y = y; WorldPos = worldPos; Walkable = walkable;
        GValue = float.PositiveInfinity;
        HValue = 0f;
        CameFrom = null;
    }

    public float F() => GValue + HValue;
}
