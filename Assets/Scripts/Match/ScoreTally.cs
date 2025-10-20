using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreTally : MonoBehaviour
{
    private Dictionary<int, int> playerScores = new();

    public void DeterminePlacements(List<PlayerInput> players)
    {
      playerScores.Clear();
      Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);

      foreach (var plant in allPlants)
      {
         if (plant.ownerPlayerIndex < 0) continue;

         int points = plant.GetScoring();

         if (!playerScores.ContainsKey(plant.ownerPlayerIndex))
               playerScores[plant.ownerPlayerIndex] = 0;

         playerScores[plant.ownerPlayerIndex] += points;
      }

      // Sort by score descending
      var placements = playerScores.OrderByDescending(p => p.Value).ToList();

      Debug.Log("=== Final Scores ===");
      foreach (var p in placements)
      {
         var player = players.FirstOrDefault(x => x.playerIndex == p.Key);
         Debug.Log($"Player {p.Key} ({player?.name ?? "?"}) → {p.Value} points");
      }

      var winner = placements.FirstOrDefault();
      Debug.Log($"Winner: Player {winner.Key} with {winner.Value} points!");
    }
}
