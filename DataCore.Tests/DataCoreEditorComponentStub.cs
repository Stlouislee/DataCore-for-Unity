// Stub for DataCoreEditorComponent (excluded from test build but referenced by CaliforniaHousingDataset.cs)

namespace AroAro.DataCore
{
    public class DataCoreEditorComponent : UnityEngine.MonoBehaviour
    {
        public static DataCoreEditorComponent Instance => null;
        public string InstanceName => "Stub";

        public DataCoreStore GetStore()
        {
            throw new System.NotSupportedException("DataCoreEditorComponent stub - not available in test context");
        }
    }
}
