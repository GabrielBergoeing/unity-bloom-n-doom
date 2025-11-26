using UnityEngine;

public abstract class ObjectEvaluator : ScriptableObject
{
    public abstract bool Function(GameObject obj, object world);
}