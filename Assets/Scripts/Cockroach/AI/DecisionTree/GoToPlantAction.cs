using UnityEngine;

[CreateAssetMenu(menuName = "AI/Actions/Go To Plant")]
public class GoToPlantAction : ActionNode
{
    private void Awake()
    {
        actionName = "GoToPlant";
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