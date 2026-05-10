using System;

namespace AroAro.DataCore.SampleDatasets
{
    /// <summary>
    /// Defines a sample dataset that can be registered and loaded by SampleDatasetManager.
    /// </summary>
    [Serializable]
    public class SampleDatasetDefinition
    {
        /// <summary>
        /// Display name for the dataset (used for logging).
        /// </summary>
        public string datasetName;

        /// <summary>
        /// Whether this dataset should be loaded automatically on Start().
        /// </summary>
        public bool loadOnStart = true;
    }
}
