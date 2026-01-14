using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace AroAro.DataCore
{
    /// <summary>
    /// Example script showing how to access the shared DataCore instance from any script
    /// </summary>
    public class DataCoreAccessExample : MonoBehaviour
    {
        private void Start()
        {
            // Access the shared DataCore instance
            if (DataCoreEditorComponent.Instance == null)
            {
                Debug.LogError("DataCoreEditorComponent not found in scene! Please add it to a GameObject.");
                return;
            }

            // Get the store from the shared instance
            var store = DataCoreEditorComponent.Instance.GetStore();

            // Example: Create and populate a tabular dataset
            var myData = store.CreateTabular("player-stats");
            myData.AddNumericColumn("score", NumSharp.np.array(new double[] { 100, 200, 300 }));
            myData.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie" });

            Debug.Log($"Created dataset with {myData.RowCount} rows");

            // Example: Query the data using the fluent API
            var results = myData.Query()
                .WhereGreaterThan("score", 150)
                .ToDictionaries()
                .ToList();
            Debug.Log($"Found {results.Count} players with score > 150");

            // Data is automatically persisted to LiteDB
        }

        public void AddPlayerScore(string playerName, double score)
        {
            // Access the shared store from anywhere
            var store = DataCoreEditorComponent.Instance?.GetStore();
            if (store == null) return;

            // Get or create the dataset
            var playerData = store.GetOrCreateTabular("player-stats");
            
            // Check if columns exist, add them if this is a new dataset
            if (!playerData.HasColumn("score"))
            {
                playerData.AddNumericColumn("score", new double[0]);
            }
            if (!playerData.HasColumn("name"))
            {
                playerData.AddStringColumn("name", new string[0]);
            }

            // Add the new row
            playerData.AddRow(new Dictionary<string, object>
            {
                { "name", playerName },
                { "score", score }
            });

            Debug.Log($"Added player {playerName} with score {score}");
        }

        public void LoadHighScores()
        {
            var store = DataCoreEditorComponent.Instance?.GetStore();
            if (store == null)
            {
                Debug.Log("Store not available");
                return;
            }

            var playerData = store.GetTabular("player-stats");
            if (playerData == null)
            {
                Debug.Log("No player stats found");
                return;
            }

            // Query for high scores using fluent API
            var highScorers = playerData.Query()
                .WhereGreaterThan("score", 500)
                .OrderByDescending("score")
                .ToDictionaries()
                .ToList();

            Debug.Log($"=== High Scores (> 500) ===");
            foreach (var scorer in highScorers)
            {
                Debug.Log($"{scorer["name"]}: {scorer["score"]}");
            }
        }
    }
}
