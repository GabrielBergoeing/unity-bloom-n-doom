using UnityEngine;

[CreateAssetMenu(menuName = "AI/Nodes/Binary Decision Node")]
public class BinaryDecisionNode : DecisionNode
{
    public Node yesNode;
    public Node noNode;
    public Decision decision;

    public override Node GetBranch(GameObject obj, object world)
    {
        return decision.Evaluate(obj, world) ? yesNode : noNode;
    }
}