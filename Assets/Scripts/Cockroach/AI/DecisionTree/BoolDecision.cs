using UnityEngine;

[CreateAssetMenu(menuName = "AI/Decisions/Bool Decision")]
public class BoolDecision : Decision
{
    public bool testValue;

    public override bool Evaluate(GameObject obj, object world)
    {
        return testValue;
    }
}