using UnityEngine;

public abstract class Node : ScriptableObject
{
    public abstract Node Decide(GameObject obj, object world);
}
