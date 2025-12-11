using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Microsoft.Data.Analysis;

namespace DataCore.UnityIntegration
{
    [System.Serializable]
    public class DataFrameEvent : UnityEvent<Microsoft.Data.Analysis.DataFrame> { }

    /// <summary>
    /// Component that loads a DataFrame by key and invokes a UnityEvent when ready
    /// </summary>
    public class DataBindingComponent : MonoBehaviour
    {
        public string DataKey;
        public DataFrameEvent OnDataLoaded;

        private async void Start()
        {
            if (string.IsNullOrEmpty(DataKey))
                return;

            var manager = DataCore.UnifiedDataManager.Instance;
            try
            {
                var df = await manager.DataFrames.LoadCsvAsync(DataKey, DataKey + ".csv");
                OnDataLoaded?.Invoke(df);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load DataFrame '{DataKey}': {ex.Message}");
            }
        }
    }
}