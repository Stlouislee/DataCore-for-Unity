using System;
using System.Collections.Generic;
using NumSharp;

namespace AroAro.DataCore.Tabular
{
    public sealed class TabularQuery
    {
        private readonly TabularData _table;
        private readonly List<Func<int, bool>> _rowPredicates = new();

        internal TabularQuery(TabularData table)
        {
            _table = table;
        }

        public TabularQuery Where(string columnName, TabularOp op, object value)
        {
            var col = _table.GetColumnInternal(columnName);

            switch (col)
            {
                case NumericColumn n:
                    var num = value == null ? double.NaN : Convert.ToDouble(value);
                    _rowPredicates.Add(i => CompareNumeric(Convert.ToDouble((object)n.Data.GetValue(i)), op, num));
                    break;
                case StringColumn s:
                    var str = value?.ToString();
                    _rowPredicates.Add(i => CompareString(s.Data[i], op, str));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported column kind for query: {col.Kind}");
            }

            return this;
        }

        public int[] ToRowIndices()
        {
            var results = new List<int>();
            for (var i = 0; i < _table.RowCount; i++)
            {
                var ok = true;
                for (var p = 0; p < _rowPredicates.Count; p++)
                {
                    if (!_rowPredicates[p](i))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok) results.Add(i);
            }

            return results.ToArray();
        }

        public NDArray ToMask()
        {
            var managed = new bool[_table.RowCount];
            var idx = ToRowIndices();
            for (var i = 0; i < idx.Length; i++)
                managed[idx[i]] = true;
            return np.array(managed);
        }

        private static bool CompareNumeric(double left, TabularOp op, double right)
        {
            return op switch
            {
                TabularOp.Eq => left == right,
                TabularOp.Ne => left != right,
                TabularOp.Gt => left > right,
                TabularOp.Ge => left >= right,
                TabularOp.Lt => left < right,
                TabularOp.Le => left <= right,
                _ => throw new NotSupportedException($"Operator not supported for numeric: {op}")
            };
        }

        private static bool CompareString(string left, TabularOp op, string right)
        {
            return op switch
            {
                TabularOp.Eq => string.Equals(left, right, StringComparison.Ordinal),
                TabularOp.Ne => !string.Equals(left, right, StringComparison.Ordinal),
                TabularOp.Contains => (left ?? string.Empty).Contains(right ?? string.Empty, StringComparison.Ordinal),
                _ => throw new NotSupportedException($"Operator not supported for string: {op}")
            };
        }
    }
}
