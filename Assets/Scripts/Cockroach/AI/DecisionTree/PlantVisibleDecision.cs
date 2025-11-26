using UnityEngine;

[CreateAssetMenu(menuName = "AI/Decisions/Plant Visible Decision")]
public class PlantVisibleDecision : BoolDecision
{
    public override bool Evaluate(GameObject obj, object world)
    {
        if (world is CockroachWorldContext context)
        {
            return context.isPlantVisible;
        }
        return false;
    }
}