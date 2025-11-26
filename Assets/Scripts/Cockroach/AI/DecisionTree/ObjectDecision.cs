using UnityEngine;

[CreateAssetMenu(menuName = "AI/Decisions/Object Decision")]
public class ObjectDecision : Decision
{
    public ObjectEvaluator evaluator;

    public override bool Evaluate(GameObject obj, object world)
    {
        return evaluator != null && evaluator.Function(obj, world);
    }
}