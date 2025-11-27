using UnityEngine;
using System.Collections.Generic;

public class CrownOfFlowers : Plant
{
    [Header("Bonus Scoring per Adjacent Mature Plant")]
    [SerializeField][Range(0, 3)] 
    private int bonusScorePerPlant = 1;

    // Directions to check around the cell (up, down, left, right)
    private static readonly Vector3Int[] neighborOffsets =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0)
    };

    public override int GetScoring()
    {
        // Base scoring from parent
        int baseScore = base.GetScoring();

        // Only mature plants get synergy
        if (stage != GrowthStage.Mature)
            return baseScore;

        int bonus = 0;

        foreach (var offset in neighborOffsets)
        {
            Vector3Int checkCell = cellPos + offset;

            if (FarmManager.instance == null) continue;

            // Ask FarmManager if there's a plant in this position
            Plant neighborPlant = FarmManager.instance.TryGetPlant(checkCell);
            if (neighborPlant == null) continue;

            // Must be mature too to give a synergy bonus
            if (neighborPlant is CrownOfFlowers || neighborPlant.stage == GrowthStage.Mature)
            {
                bonus += bonusScorePerPlant;
            }
        }

        return baseScore + bonus;
    }
}
