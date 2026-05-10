using System.Collections.Generic;
using UnityEngine;

namespace AroAro.DataCore.SampleDatasets
{
    /// <summary>
    /// ScriptableObject-based registry of sample datasets.
    /// Create via Assets menu: DataCore > Sample Dataset Registry.
    /// </summary>
    [CreateAssetMenu(fileName = "SampleDatasetRegistry", menuName = "DataCore/Sample Dataset Registry")]
    public class SampleDatasetRegistry : ScriptableObject
    {
        [SerializeField] private List<SampleDatasetDefinition> datasets = new List<SampleDatasetDefinition>();

        /// <summary>
        /// Read-only access to the registered datasets.
        /// </summary>
        public IReadOnlyList<SampleDatasetDefinition> Datasets => datasets;
    }
}
