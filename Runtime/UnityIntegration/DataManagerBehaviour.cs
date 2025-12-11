using System.Threading.Tasks;
using UnityEngine;

namespace DataCore.UnityIntegration
{
    /// <summary>
    /// MonoBehaviour wrapper that exposes the UnifiedDataManager singleton to scenes
    /// </summary>
    public class DataManagerBehaviour : MonoBehaviour
    {
        public DataCore.UnifiedDataManager DataManager { get; private set; }

        private async void Awake()
        {
            DataManager = DataCore.UnifiedDataManager.Instance;
            await Task.Yield();
        }

        private void OnDestroy()
        {
            // Best-effort: save all datasets when the component is destroyed
            var _ = DataManager?.SaveAllAsync();
        }
    }
}