using System;

namespace AroAro.DataCore
{
    public enum DataSetKind
    {
        Tabular = 1,
        Graph = 2,
    }

    public interface IDataSet
    {
        string Name { get; }
        DataSetKind Kind { get; }

        /// <summary>Returns a dataset copy with a different name.</summary>
        IDataSet WithName(string name);
    }
}
