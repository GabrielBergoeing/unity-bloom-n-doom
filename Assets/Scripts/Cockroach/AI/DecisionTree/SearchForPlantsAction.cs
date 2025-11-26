using UnityEngine;

[CreateAssetMenu(menuName = "AI/Actions/Search For Plants")]
public class SearchForPlantsAction : ActionNode
{
    private void Awake()
    {
        actionName = "SearchForPlants";
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