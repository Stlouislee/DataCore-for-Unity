using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace AroAro.DataCore.LiteDb
{
    /// <summary>
    /// LiteDB 表格查询实现
    /// </summary>
    public sealed class LiteDbTabularQuery : ITabularQuery
    {
        private readonly LiteDbTabularDataset _dataset;
        private readonly List<Func<TabularRow, bool>> _filters;
        private string _orderByColumn;
        private bool _orderDescending;
        private int _skip;
        private int _limit = int.MaxValue;
        private List<string> _selectColumns;

        internal LiteDbTabularQuery(LiteDbTabularDataset dataset)
        {
            _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
            _filters = new List<Func<TabularRow, bool>>();
            _selectColumns = new List<string>();
        }

        #region ITabularQuery 实现

        public ITabularQuery Select(params string[] columns)
        {
            _selectColumns.AddRange(columns);
            return this;
        }

        public ITabularQuery Where(string column, QueryOp op, object value)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue))
                    return false;

                return BsonValueComparer.Evaluate(bsonValue, op, value);
            });
            return this;
        }

        public ITabularQuery WhereEquals(string column, object value)
            => Where(column, QueryOp.Eq, value);

        public ITabularQuery WhereNotEquals(string column, object value)
            => Where(column, QueryOp.Ne, value);

        public ITabularQuery WhereGreaterThan(string column, double value)
            => Where(column, QueryOp.Gt, value);

        public ITabularQuery WhereGreaterThanOrEqual(string column, double value)
            => Where(column, QueryOp.Ge, value);

        public ITabularQuery WhereLessThan(string column, double value)
            => Where(column, QueryOp.Lt, value);

        public ITabularQuery WhereLessThanOrEqual(string column, double value)
            => Where(column, QueryOp.Le, value);

        public ITabularQuery WhereBetween(string column, double min, double max)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue) || !bsonValue.IsNumber)
                    return false;
                var val = bsonValue.AsDouble;
                return val >= min && val <= max;
            });
            return this;
        }

        public ITabularQuery WhereContains(string column, string value)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue) || !bsonValue.IsString)
                    return false;
                return bsonValue.AsString?.Contains(value) ?? false;
            });
            return this;
        }

        public ITabularQuery WhereStartsWith(string column, string value)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue) || !bsonValue.IsString)
                    return false;
                return bsonValue.AsString?.StartsWith(value) ?? false;
            });
            return this;
        }

        public ITabularQuery WhereIn<T>(string column, IEnumerable<T> values)
        {
            var valueSet = new HashSet<object>(values.Cast<object>());
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue))
                    return false;
                var val = BsonValueConverter.FromBsonValue(bsonValue);
                return valueSet.Contains(val);
            });
            return this;
        }

        public ITabularQuery WhereIsNull(string column)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue))
                    return true;
                return bsonValue.IsNull;
            });
            return this;
        }

        public ITabularQuery WhereIsNotNull(string column)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue))
                    return false;
                return !bsonValue.IsNull;
            });
            return this;
        }

        public ITabularQuery OrderBy(string column)
        {
            _orderByColumn = column;
            _orderDescending = false;
            return this;
        }

        public ITabularQuery OrderByDescending(string column)
        {
            _orderByColumn = column;
            _orderDescending = true;
            return this;
        }

        public ITabularQuery Skip(int count)
        {
            _skip = Math.Max(0, count);
            return this;
        }

        public ITabularQuery Limit(int count)
        {
            _limit = Math.Max(0, count);
            return this;
        }

        public ITabularQuery Page(int pageNumber, int pageSize)
        {
            _skip = (pageNumber - 1) * pageSize;
            _limit = pageSize;
            return this;
        }

        #endregion

        #region 执行查询

        public int Count()
        {
            return ExecuteFilters().Count();
        }

        public bool Any()
        {
            return ExecuteFilters().Any();
        }

        public List<Dictionary<string, object>> ToDictionaries()
        {
            var rows = ExecuteFilters();

            if (!string.IsNullOrEmpty(_orderByColumn))
            {
                rows = _orderDescending
                    ? rows.OrderByDescending(r => GetSortValue(r, _orderByColumn))
                    : rows.OrderBy(r => GetSortValue(r, _orderByColumn));
            }

            rows = rows.Skip(_skip).Take(_limit);

            var columns = _selectColumns.Count > 0 ? _selectColumns : _dataset.ColumnNames.ToList();

            var result = new List<Dictionary<string, object>>();
            foreach (var row in rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (var col in columns)
                {
                    if (row.Data.TryGetValue(col, out var bsonValue))
                    {
                        dict[col] = BsonValueConverter.FromBsonValue(bsonValue);
                    }
                    else
                    {
                        dict[col] = null;
                    }
                }
                result.Add(dict);
            }
            return result;
        }

        public int[] ToRowIndices()
        {
            var rows = ExecuteFilters();

            if (!string.IsNullOrEmpty(_orderByColumn))
            {
                rows = _orderDescending
                    ? rows.OrderByDescending(r => GetSortValue(r, _orderByColumn))
                    : rows.OrderBy(r => GetSortValue(r, _orderByColumn));
            }

            return rows.Skip(_skip).Take(_limit).Select(r => r.RowIndex).ToArray();
        }

        public Dictionary<string, object> FirstOrDefault()
        {
            var rows = ExecuteFilters();

            if (!string.IsNullOrEmpty(_orderByColumn))
            {
                rows = _orderDescending
                    ? rows.OrderByDescending(r => GetSortValue(r, _orderByColumn))
                    : rows.OrderBy(r => GetSortValue(r, _orderByColumn));
            }

            var row = rows.FirstOrDefault();
            if (row == null) return null;

            var columns = _selectColumns.Count > 0 ? _selectColumns : _dataset.ColumnNames.ToList();
            var dict = new Dictionary<string, object>();
            foreach (var col in columns)
            {
                if (row.Data.TryGetValue(col, out var bsonValue))
                {
                    dict[col] = BsonValueConverter.FromBsonValue(bsonValue);
                }
                else
                {
                    dict[col] = null;
                }
            }
            return dict;
        }

        #endregion

        #region 聚合函数

        public double Sum(string column)
        {
            return ExecuteFilters()
                .Select(r => r.Data.TryGetValue(column, out var v) && v.IsNumber ? v.AsDouble : 0.0)
                .Sum();
        }

        public double Average(string column)
        {
            var values = ExecuteFilters()
                .Select(r => r.Data.TryGetValue(column, out var v) && v.IsNumber ? v.AsDouble : (double?)null)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();

            return values.Count > 0 ? values.Average() : 0.0;
        }

        public double Max(string column)
        {
            var values = ExecuteFilters()
                .Select(r => r.Data.TryGetValue(column, out var v) && v.IsNumber ? v.AsDouble : (double?)null)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();

            return values.Count > 0 ? values.Max() : 0.0;
        }

        public double Min(string column)
        {
            var values = ExecuteFilters()
                .Select(r => r.Data.TryGetValue(column, out var v) && v.IsNumber ? v.AsDouble : (double?)null)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();

            return values.Count > 0 ? values.Min() : 0.0;
        }

        #endregion

        #region 内部方法

        private IEnumerable<TabularRow> ExecuteFilters()
        {
            var rows = _dataset.GetAllRowsInternal();

            foreach (var filter in _filters)
            {
                rows = rows.Where(filter);
            }

            return rows;
        }

        private object GetSortValue(TabularRow row, string column)
        {
            if (!row.Data.TryGetValue(column, out var bsonValue))
                return null;

            return BsonValueConverter.FromBsonValue(bsonValue);
        }

        #endregion
    }
}
