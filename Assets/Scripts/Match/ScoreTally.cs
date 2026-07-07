using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreTally : MonoBehaviour
{
   private Dictionary<int, int> playerScores = new();

   public List<ScoreResult> DeterminePlacements(List<PlayerInput> players)
   {
      playerScores.Clear();

      // Gather all plants on the map
      Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);

      foreach (var plant in allPlants)
      {
         if (plant.ownerPlayerIndex < 0)
               continue; // skip unowned plants

         int points = plant.GetScoring();

         if (!playerScores.ContainsKey(plant.ownerPlayerIndex))
               playerScores[plant.ownerPlayerIndex] = 0;

         playerScores[plant.ownerPlayerIndex] += points;
      }

      // Sort highest score first
      var placements = playerScores.OrderByDescending(p => p.Value);

      // Convert into serializable result objects
      List<ScoreResult> results = new();
      foreach (var pair in placements)
      {
         var player = players.FirstOrDefault(x => x.playerIndex == pair.Key);

         results.Add(new ScoreResult
         {
               playerIndex = pair.Key,
               playerName = player != null ? player.name : $"Player {pair.Key}",
               score = pair.Value
         });
      }

      return results;
   }

   public List<ScoreResult> DeterminePlacementsByIndex(IReadOnlyDictionary<int, string> playerNames)
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

      var placements = playerScores.OrderByDescending(p => p.Value);
      List<ScoreResult> results = new();
      foreach (var pair in placements)
      {
         string name = (playerNames != null && playerNames.TryGetValue(pair.Key, out string n))
            ? n : $"Player {pair.Key}";
         results.Add(new ScoreResult { playerIndex = pair.Key, playerName = name, score = pair.Value });
      }
      return results;
   }
}
