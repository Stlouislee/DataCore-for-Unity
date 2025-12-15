using UnityEngine;
using System.Linq;

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

            // Example: Query the data
            var query = myData.Query().Where("score", Tabular.TabularOp.Gt, 150);
            var indices = query.ToRowIndices();
            Debug.Log($"Found {indices.Length} players with score > 150");

            // The dataset will be automatically saved when the scene ends
            // Or you can manually save it:
            // DataCoreEditorComponent.Instance.SaveDataset("player-stats");
        }

        public void AddPlayerScore(string playerName, double score)
        {
            // Access the shared store from anywhere
            var store = DataCoreEditorComponent.Instance?.GetStore();
            if (store == null) return;

            // Get or create the dataset
            if (!store.TryGet("player-stats", out var dataset))
            {
                var newData = store.CreateTabular("player-stats");
                newData.AddNumericColumn("score", NumSharp.np.array(new double[] { }));
                newData.AddStringColumn("name", new string[] { });
                dataset = newData;
            }

            var playerData = dataset as Tabular.TabularData;
            if (playerData != null)
            {
                playerData.AddRow(new System.Collections.Generic.Dictionary<string, object>
                {
                    { "name", playerName },
                    { "score", score }
                });

                Debug.Log($"Added player {playerName} with score {score}");
            }
        }

        public void LoadHighScores()
        {
            var store = DataCoreEditorComponent.Instance?.GetStore();
            if (store == null || !store.TryGet("player-stats", out var dataset))
            {
                Debug.Log("No player stats found");
                return;
            }

            var playerData = dataset as Tabular.TabularData;
            if (playerData != null)
            {
                // Query for high scores
                var highScorerIndices = playerData.Query()
                    .Where("score", Tabular.TabularOp.Gt, 500)
                    .ToRowIndices();

                var names = playerData.GetStringColumn("name");
                var scores = playerData.GetNumericColumn("score");

                // Sort manually since OrderBy is not available
                var highScorers = highScorerIndices
                    .Select(idx => new { Name = names[idx], Score = scores[idx], Index = idx })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                Debug.Log($"=== High Scores (> 500) ===");
                foreach (var scorer in highScorers)
                {
                    Debug.Log($"{scorer.Name}: {scorer.Score}");
                }
            }
        }
    }
}
