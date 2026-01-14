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

                return EvaluateCondition(bsonValue, op, value);
            });
            return this;
        }

        public ITabularQuery WhereEquals(string column, object value)
            => Where(column, QueryOp.Equal, value);

        public ITabularQuery WhereNotEquals(string column, object value)
            => Where(column, QueryOp.NotEqual, value);

        public ITabularQuery WhereGreaterThan(string column, double value)
            => Where(column, QueryOp.GreaterThan, value);

        public ITabularQuery WhereGreaterOrEqual(string column, double value)
            => Where(column, QueryOp.GreaterOrEqual, value);

        public ITabularQuery WhereLessThan(string column, double value)
            => Where(column, QueryOp.LessThan, value);

        public ITabularQuery WhereLessOrEqual(string column, double value)
            => Where(column, QueryOp.LessOrEqual, value);

        public ITabularQuery WhereBetween(string column, double minValue, double maxValue)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue) || !bsonValue.IsNumber)
                    return false;
                var val = bsonValue.AsDouble;
                return val >= minValue && val <= maxValue;
            });
            return this;
        }

        public ITabularQuery WhereIn(string column, IEnumerable<object> values)
        {
            var valueSet = new HashSet<object>(values);
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue))
                    return false;
                var val = ConvertFromBsonValue(bsonValue);
                return valueSet.Contains(val);
            });
            return this;
        }

        public ITabularQuery WhereNotIn(string column, IEnumerable<object> values)
        {
            var valueSet = new HashSet<object>(values);
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue))
                    return true;
                var val = ConvertFromBsonValue(bsonValue);
                return !valueSet.Contains(val);
            });
            return this;
        }

        public ITabularQuery WhereContains(string column, string substring)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue) || !bsonValue.IsString)
                    return false;
                return bsonValue.AsString?.Contains(substring) ?? false;
            });
            return this;
        }

        public ITabularQuery WhereStartsWith(string column, string prefix)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue) || !bsonValue.IsString)
                    return false;
                return bsonValue.AsString?.StartsWith(prefix) ?? false;
            });
            return this;
        }

        public ITabularQuery WhereEndsWith(string column, string suffix)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue) || !bsonValue.IsString)
                    return false;
                return bsonValue.AsString?.EndsWith(suffix) ?? false;
            });
            return this;
        }

        public ITabularQuery WhereNull(string column)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue))
                    return true;
                return bsonValue.IsNull;
            });
            return this;
        }

        public ITabularQuery WhereNotNull(string column)
        {
            _filters.Add(row =>
            {
                if (!row.Data.TryGetValue(column, out var bsonValue))
                    return false;
                return !bsonValue.IsNull;
            });
            return this;
        }

        public ITabularQuery OrderBy(string column, bool descending = false)
        {
            _orderByColumn = column;
            _orderDescending = descending;
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

        #endregion

        #region 执行查询

        public int Count()
        {
            return ExecuteFilters().Count();
        }

        public IEnumerable<Dictionary<string, object>> ToResults()
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

            foreach (var row in rows)
            {
                var result = new Dictionary<string, object>();
                foreach (var col in columns)
                {
                    if (row.Data.TryGetValue(col, out var bsonValue))
                    {
                        result[col] = ConvertFromBsonValue(bsonValue);
                    }
                    else
                    {
                        result[col] = null;
                    }
                }
                yield return result;
            }
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
            return ToResults().FirstOrDefault();
        }

        #endregion

        #region 聚合函数

        public double Sum(string column)
        {
            return ExecuteFilters()
                .Select(r => r.Data.TryGetValue(column, out var v) && v.IsNumber ? v.AsDouble : 0.0)
                .Sum();
        }

        public double Mean(string column)
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

        public IEnumerable<IGrouping<object, Dictionary<string, object>>> GroupBy(string column)
        {
            var results = ToResults().ToList();
            return results.GroupBy(r => r.ContainsKey(column) ? r[column] : null);
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

        private bool EvaluateCondition(BsonValue bsonValue, QueryOp op, object value)
        {
            switch (op)
            {
                case QueryOp.Equal:
                    return BsonValueEquals(bsonValue, value);

                case QueryOp.NotEqual:
                    return !BsonValueEquals(bsonValue, value);

                case QueryOp.GreaterThan:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble > Convert.ToDouble(value);

                case QueryOp.GreaterOrEqual:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble >= Convert.ToDouble(value);

                case QueryOp.LessThan:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble < Convert.ToDouble(value);

                case QueryOp.LessOrEqual:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble <= Convert.ToDouble(value);

                case QueryOp.Contains:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.Contains(value?.ToString()) ?? false;

                case QueryOp.StartsWith:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.StartsWith(value?.ToString()) ?? false;

                case QueryOp.EndsWith:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.EndsWith(value?.ToString()) ?? false;

                case QueryOp.IsNull:
                    return bsonValue.IsNull;

                case QueryOp.IsNotNull:
                    return !bsonValue.IsNull;

                default:
                    return false;
            }
        }

        private bool BsonValueEquals(BsonValue bsonValue, object value)
        {
            if (value == null) return bsonValue.IsNull;
            if (bsonValue.IsNull) return false;

            if (bsonValue.IsNumber && (value is int || value is long || value is float || value is double))
                return Math.Abs(bsonValue.AsDouble - Convert.ToDouble(value)) < 0.0001;

            if (bsonValue.IsString && value is string s)
                return bsonValue.AsString == s;

            if (bsonValue.IsBoolean && value is bool b)
                return bsonValue.AsBoolean == b;

            return bsonValue.ToString() == value.ToString();
        }

        private object GetSortValue(TabularRow row, string column)
        {
            if (!row.Data.TryGetValue(column, out var bsonValue))
                return null;

            return ConvertFromBsonValue(bsonValue);
        }

        private object ConvertFromBsonValue(BsonValue value)
        {
            if (value.IsNull) return null;
            if (value.IsDouble) return value.AsDouble;
            if (value.IsInt32) return value.AsInt32;
            if (value.IsInt64) return value.AsInt64;
            if (value.IsBoolean) return value.AsBoolean;
            if (value.IsDateTime) return value.AsDateTime;
            if (value.IsString) return value.AsString;
            return value.ToString();
        }

        #endregion
    }
}
