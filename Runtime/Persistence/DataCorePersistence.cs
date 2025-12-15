using System;
using System.IO;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Tabular;

namespace AroAro.DataCore.Persistence
{
    public static class DataCorePersistence
    {
        public static byte[] Serialize(IDataSet dataSet, string pathHint)
        {
            if (dataSet == null) throw new ArgumentNullException(nameof(dataSet));
            var ext = GetExtension(pathHint);

            return dataSet.Kind switch
            {
                DataSetKind.Tabular => ext == ".arrow" ? ArrowTabularSerializer.Serialize((TabularData)dataSet) : throw new NotSupportedException("Tabular persistence requires .arrow"),
                DataSetKind.Graph => ext == ".dcgraph" ? GraphJsonSerializer.Serialize((GraphData)dataSet) : throw new NotSupportedException("Graph persistence requires .dcgraph"),
                _ => throw new NotSupportedException($"Unknown dataset kind: {dataSet.Kind}"),
            };
        }

        public static IDataSet Deserialize(byte[] bytes, string pathHint)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var ext = GetExtension(pathHint);

            return ext switch
            {
                ".arrow" => ArrowTabularSerializer.Deserialize(bytes),
                ".dcgraph" => GraphJsonSerializer.Deserialize(bytes),
                _ => throw new NotSupportedException($"Unknown persistence extension: {ext}"),
            };
        }

        private static string GetExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Path.GetExtension(path).ToLowerInvariant();
        }
    }
}
