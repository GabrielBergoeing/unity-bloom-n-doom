using UnityEngine;

[CreateAssetMenu(menuName = "AI/Actions/Flee From Player")]
public class FleeFromPlayerAction : ActionNode
{
    private void Awake()
    {
        actionName = "FleeFromPlayer";
    }
    
    public override Node Decide(GameObject obj, object world)
    {
        if (obj.TryGetComponent(out Cockroach cockroach))
        {
            cockroach.ExecuteAction(actionName);
        }
        return this;
    }
}