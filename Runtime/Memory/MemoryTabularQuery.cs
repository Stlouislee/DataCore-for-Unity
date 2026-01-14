using System;
using System.Collections.Generic;
using System.Linq;

namespace AroAro.DataCore.Memory
{
    /// <summary>
    /// 内存表格查询实现
    /// </summary>
    public sealed class MemoryTabularQuery : ITabularQuery
    {
        private readonly MemoryTabularDataset _table;
        private readonly List<Func<int, bool>> _predicates = new();
        private readonly List<(string Column, bool Descending)> _orderBy = new();
        private int? _skip;
        private int? _limit;
        private List<string> _selectColumns;

        internal MemoryTabularQuery(MemoryTabularDataset table)
        {
            _table = table;
        }

        #region 过滤条件

        public ITabularQuery WhereEquals(string column, object value)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => Equals(col.Data[i], value));
            return this;
        }

        public ITabularQuery WhereNotEquals(string column, object value)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => !Equals(col.Data[i], value));
            return this;
        }

        public ITabularQuery WhereGreaterThan(string column, double value)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => Convert.ToDouble(col.Data[i] ?? 0) > value);
            return this;
        }

        public ITabularQuery WhereGreaterThanOrEqual(string column, double value)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => Convert.ToDouble(col.Data[i] ?? 0) >= value);
            return this;
        }

        public ITabularQuery WhereLessThan(string column, double value)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => Convert.ToDouble(col.Data[i] ?? 0) < value);
            return this;
        }

        public ITabularQuery WhereLessThanOrEqual(string column, double value)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => Convert.ToDouble(col.Data[i] ?? 0) <= value);
            return this;
        }

        public ITabularQuery WhereBetween(string column, double min, double max)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i =>
            {
                var v = Convert.ToDouble(col.Data[i] ?? 0);
                return v >= min && v <= max;
            });
            return this;
        }

        public ITabularQuery WhereContains(string column, string value)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => (col.Data[i]?.ToString() ?? "").Contains(value ?? "", StringComparison.Ordinal));
            return this;
        }

        public ITabularQuery WhereStartsWith(string column, string value)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => (col.Data[i]?.ToString() ?? "").StartsWith(value ?? "", StringComparison.Ordinal));
            return this;
        }

        public ITabularQuery WhereIn<T>(string column, IEnumerable<T> values)
        {
            var col = _table.GetColumnInternal(column);
            var set = new HashSet<object>(values.Cast<object>());
            _predicates.Add(i => set.Contains(col.Data[i]));
            return this;
        }

        public ITabularQuery WhereIsNull(string column)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => col.Data[i] == null);
            return this;
        }

        public ITabularQuery WhereIsNotNull(string column)
        {
            var col = _table.GetColumnInternal(column);
            _predicates.Add(i => col.Data[i] != null);
            return this;
        }

        public ITabularQuery Where(string column, QueryOp op, object value)
        {
            return op switch
            {
                QueryOp.Eq => WhereEquals(column, value),
                QueryOp.Ne => WhereNotEquals(column, value),
                QueryOp.Gt => WhereGreaterThan(column, Convert.ToDouble(value)),
                QueryOp.Ge => WhereGreaterThanOrEqual(column, Convert.ToDouble(value)),
                QueryOp.Lt => WhereLessThan(column, Convert.ToDouble(value)),
                QueryOp.Le => WhereLessThanOrEqual(column, Convert.ToDouble(value)),
                QueryOp.Contains => WhereContains(column, value?.ToString()),
                QueryOp.StartsWith => WhereStartsWith(column, value?.ToString()),
                _ => throw new NotSupportedException($"Operator {op} is not supported")
            };
        }

        #endregion

        #region 排序

        public ITabularQuery OrderBy(string column)
        {
            _orderBy.Add((column, false));
            return this;
        }

        public ITabularQuery OrderByDescending(string column)
        {
            _orderBy.Add((column, true));
            return this;
        }

        #endregion

        #region 分页

        public ITabularQuery Skip(int count)
        {
            _skip = count;
            return this;
        }

        public ITabularQuery Limit(int count)
        {
            _limit = count;
            return this;
        }

        public ITabularQuery Page(int pageNumber, int pageSize)
        {
            _skip = (pageNumber - 1) * pageSize;
            _limit = pageSize;
            return this;
        }

        #endregion

        #region 列选择

        public ITabularQuery Select(params string[] columns)
        {
            _selectColumns = columns.ToList();
            return this;
        }

        #endregion

        #region 执行查询

        public int[] ToRowIndices()
        {
            var indices = Enumerable.Range(0, _table.RowCount)
                .Where(i => _predicates.All(p => p(i)));

            // 排序
            if (_orderBy.Count > 0)
            {
                var first = _orderBy[0];
                var col = _table.GetColumnInternal(first.Column);
                indices = first.Descending
                    ? indices.OrderByDescending(i => col.Data[i])
                    : indices.OrderBy(i => col.Data[i]);
            }

            // 分页
            if (_skip.HasValue)
                indices = indices.Skip(_skip.Value);
            if (_limit.HasValue)
                indices = indices.Take(_limit.Value);

            return indices.ToArray();
        }

        public List<Dictionary<string, object>> ToDictionaries()
        {
            var indices = ToRowIndices();
            var result = new List<Dictionary<string, object>>();
            var columns = _selectColumns ?? _table.ColumnNames.ToList();

            foreach (var i in indices)
            {
                var row = _table.GetRow(i);
                if (_selectColumns != null)
                {
                    row = row.Where(kv => _selectColumns.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value);
                }
                result.Add(row);
            }

            return result;
        }

        public int Count() => ToRowIndices().Length;

        public bool Any() => ToRowIndices().Length > 0;

        public Dictionary<string, object> FirstOrDefault()
        {
            _limit = 1;
            var results = ToDictionaries();
            return results.Count > 0 ? results[0] : null;
        }

        #endregion

        #region 聚合函数

        public double Sum(string column)
        {
            var indices = ToRowIndices();
            var col = _table.GetColumnInternal(column);
            return indices.Sum(i => Convert.ToDouble(col.Data[i] ?? 0));
        }

        public double Average(string column)
        {
            var indices = ToRowIndices();
            if (indices.Length == 0) return 0;
            var col = _table.GetColumnInternal(column);
            return indices.Average(i => Convert.ToDouble(col.Data[i] ?? 0));
        }

        public double Max(string column)
        {
            var indices = ToRowIndices();
            if (indices.Length == 0) return double.NaN;
            var col = _table.GetColumnInternal(column);
            return indices.Max(i => Convert.ToDouble(col.Data[i] ?? double.MinValue));
        }

        public double Min(string column)
        {
            var indices = ToRowIndices();
            if (indices.Length == 0) return double.NaN;
            var col = _table.GetColumnInternal(column);
            return indices.Min(i => Convert.ToDouble(col.Data[i] ?? double.MaxValue));
        }

        #endregion
    }
}
