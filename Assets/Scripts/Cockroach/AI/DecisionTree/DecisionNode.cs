using UnityEngine;

public abstract class DecisionNode : Node
{
    public abstract Node GetBranch(GameObject obj, object world);

    public override Node Decide(GameObject obj, object world)
    {
        return GetBranch(obj, world).Decide(obj, world);
    }
}
