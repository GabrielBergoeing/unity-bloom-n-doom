using UnityEngine;

[CreateAssetMenu(menuName = "AI/Decisions/Player Near Decision")]
public class PlayerNearDecision : BoolDecision
{
    public override bool Evaluate(GameObject obj, object world)
    {
        if (world is CockroachWorldContext context)
        {
            return context.isPlayerNear;
        }
        return false;
    }
}