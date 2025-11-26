using UnityEngine;

[CreateAssetMenu(menuName = "AI/Decisions/Double Decision")]
public class DoubleDecision : Decision
{
    public float minValue;
    public float maxValue;
    public float testValue;

    public override bool Evaluate(GameObject obj, object world)
    {
        return testValue >= minValue && testValue <= maxValue;
    }
}