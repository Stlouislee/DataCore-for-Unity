using UnityEngine;
using DataCore;
using DataCore.DataFrame;
using DataCore.UnityIntegration;

namespace DataCore.Samples
{
    /// <summary>
    /// Example script demonstrating DataFrame operations with DataCore
    /// </summary>
    public class DataFrameExampleScript : MonoBehaviour
    {
        [SerializeField] private DataManagerBehaviour dataManager;
        
        private void Start()
        {
            if (dataManager == null)
            {
                dataManager = FindObjectOfType<DataManagerBehaviour>();
            }
            
            if (dataManager != null && dataManager.DataManager != null)
            {
                RunDataFrameExample();
            }
            else
            {
                Debug.LogError("DataManager not found!");
            }
        }
        
        private async void RunDataFrameExample()
        {
            var dfManager = dataManager.DataManager.DataFrameManager;
            
            // Example 1: Create DataFrame with sample data
            Debug.Log("=== DataFrame Operations Example ===");
            
            var df = dfManager.CreateDataFrame("example_df");
            
            // Add columns
            df.AddColumn("Name", new string[] { "Alice", "Bob", "Charlie", "Diana" });
            df.AddColumn("Age", new int[] { 25, 30, 35, 28 });
            df.AddColumn("Score", new float[] { 85.5f, 92.0f, 78.5f, 88.0f });
            df.AddColumn("Active", new bool[] { true, true, false, true });
            
            Debug.Log("Original DataFrame:");
            Debug.Log(df.ToString());
            
            // Example 2: Filter data
            var filteredDf = dfManager.Filter(df, "Age > 25");
            Debug.Log("Filtered DataFrame (Age > 25):");
            Debug.Log(filteredDf.ToString());
            
            // Example 3: Group by and aggregate
            var groupedDf = dfManager.GroupBy(df, "Active", "Score", "mean");
            Debug.Log("Grouped DataFrame (by Active, mean Score):");
            Debug.Log(groupedDf.ToString());
            
            // Example 4: Save and load DataFrame
            await dfManager.SaveDataFrameAsync(df, "dataframes/example.csv");
            Debug.Log("DataFrame saved to: dataframes/example.csv");
            
            var loadedDf = await dfManager.LoadDataFrameAsync("dataframes/example.csv");
            Debug.Log("Loaded DataFrame:");
            Debug.Log(loadedDf.ToString());
            
            // Example 5: Join operations
            var df2 = dfManager.CreateDataFrame("example_df2");
            df2.AddColumn("Name", new string[] { "Alice", "Bob", "Eve" });
            df2.AddColumn("Department", new string[] { "Engineering", "Marketing", "Sales" });
            
            var joinedDf = dfManager.Join(df, df2, "Name", "inner");
            Debug.Log("Joined DataFrame:");
            Debug.Log(joinedDf.ToString());
            
            Debug.Log("DataFrame example completed!");
        }
    }
}