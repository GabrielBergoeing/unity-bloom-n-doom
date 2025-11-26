using UnityEngine;

public abstract class Decision : ScriptableObject
{
    public abstract bool Evaluate(GameObject obj, object world);
}