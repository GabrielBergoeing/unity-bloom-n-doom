using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreTally : MonoBehaviour
{
   private Dictionary<int, int> playerScores = new();

   /// <summary>
   /// Server-side scoring for online matches: tallies every plant against the
   /// spawned NetworkPlayers (index + chosen character).
   /// </summary>
   public static List<ScoreResult> ComputeNetworkResults()
   {
      var scores = new Dictionary<int, int>();
      var characterIds = new Dictionary<int, int>();

      // Seed every connected player so 0-score players still appear.
      foreach (var np in NetworkPlayer.All)
      {
         scores[np.Index] = 0;
         characterIds[np.Index] = np.characterId.Value;
      }

      foreach (var plant in FindObjectsByType<Plant>(FindObjectsSortMode.None))
      {
         if (plant.ownerPlayerIndex < 0) continue;
         if (!scores.ContainsKey(plant.ownerPlayerIndex))
            scores[plant.ownerPlayerIndex] = 0;
         scores[plant.ownerPlayerIndex] += plant.GetScoring();
      }

      var results = new List<ScoreResult>();
      foreach (var pair in scores.OrderByDescending(p => p.Value))
      {
         results.Add(new ScoreResult
         {
            playerIndex = pair.Key,
            playerName = $"Player {pair.Key + 1}",
            score = pair.Value,
            characterId = characterIds.TryGetValue(pair.Key, out var charId) ? charId : pair.Key
         });
      }

      return results;
   }

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
               score = pair.Value,
               characterId = pair.Key
         });
      }

      return results;
   }
}
