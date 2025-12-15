using System;
using System.Collections.Generic;
using System.IO;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using AroAro.DataCore.Tabular;
using NumSharp;

namespace AroAro.DataCore.Persistence
{
    public static class ArrowTabularSerializer
    {
        public static byte[] Serialize(TabularData table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var fields = new List<Field>();
            var arrays = new List<IArrowArray>();

            foreach (var colName in table.ColumnNames)
            {
                // Use public getters by probing internal type via query.
                // We intentionally support numeric (float64) and string.
                if (TryGetNumeric(table, colName, out var n))
                {
                    var arr = BuildFloat64Array(n);
                    arrays.Add(arr);
                    fields.Add(new Field(colName, arr.Data.DataType, nullable: true));
                }
                else if (TryGetString(table, colName, out var s))
                {
                    var arr = BuildStringArray(s);
                    arrays.Add(arr);
                    fields.Add(new Field(colName, arr.Data.DataType, nullable: true));
                }
                else
                {
                    throw new NotSupportedException($"Unsupported column for Arrow: {colName}");
                }
            }

            var schema = new Schema(fields, metadata: null);
            var batch = new RecordBatch(schema, arrays, table.RowCount);

            using var ms = new MemoryStream();
            using (var writer = new ArrowStreamWriter(ms, schema))
            {
                writer.WriteRecordBatchAsync(batch).GetAwaiter().GetResult();
                writer.WriteEndAsync().GetAwaiter().GetResult();
            }

            return ms.ToArray();
        }

        public static TabularData Deserialize(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using var ms = new MemoryStream(bytes);
            using var reader = new ArrowStreamReader(ms);

            var schema = reader.Schema;
            RecordBatch batch = reader.ReadNextRecordBatchAsync().GetAwaiter().GetResult();
            if (batch == null) throw new InvalidOperationException("Arrow stream contains no record batch");

            var t = new TabularData("table");

            for (var i = 0; i < schema.FieldsList.Count; i++)
            {
                var field = schema.GetFieldByIndex(i);
                var arr = batch.Column(i);

                if (arr is DoubleArray d)
                {
                    var managed = new double[d.Length];
                    for (var r = 0; r < d.Length; r++)
                        managed[r] = d.IsValid(r) ? d.GetValue(r).GetValueOrDefault() : double.NaN;
                    t.AddNumericColumn(field.Name, np.array(managed));
                    continue;
                }

                if (arr is StringArray s)
                {
                    var managed = new string[s.Length];
                    for (var r = 0; r < s.Length; r++)
                        managed[r] = s.IsValid(r) ? s.GetString(r) : null;
                    t.AddStringColumn(field.Name, managed);
                    continue;
                }

                throw new NotSupportedException($"Unsupported Arrow array type: {arr.GetType().Name} (field {field.Name})");
            }

            return t;
        }

        private static bool TryGetNumeric(TabularData t, string name, out NDArray data)
        {
            try
            {
                data = t.GetNumericColumn(name);
                // Persist as float64.
                data = data.astype(np.float64);
                return true;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        private static bool TryGetString(TabularData t, string name, out string[] data)
        {
            try
            {
                data = t.GetStringColumn(name);
                return true;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        private static IArrowArray BuildFloat64Array(NDArray data)
        {
            var builder = new DoubleArray.Builder();
            var len = (int)data.size;
            for (var i = 0; i < len; i++)
            {
                var v = Convert.ToDouble((object)data.GetValue(i));
                if (double.IsNaN(v)) builder.AppendNull();
                else builder.Append(v);
            }
            return builder.Build();
        }

        private static IArrowArray BuildStringArray(string[] data)
        {
            var builder = new StringArray.Builder();
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] == null) builder.AppendNull();
                else builder.Append(data[i]);
            }
            return builder.Build();
        }
    }
}
