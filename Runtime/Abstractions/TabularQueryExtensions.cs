using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AroAro.DataCore
{
    /// <summary>
    /// ITabularQuery 扩展方法 — 提供 LINQ-like lambda 语法。
    /// </summary>
    /// <example>
    /// <code>
    /// var result = dataset.Query()
    ///     .Where(row => row.Get&lt;float&gt;("Revenue") > 1000f)
    ///     .Where(row => row.Get&lt;string&gt;("Region") == "APAC")
    ///     .OrderBy("Revenue", SortDirection.Descending)
    ///     .Limit(100)
    ///     .Execute();
    /// </code>
    /// </example>
    public static class TabularQueryExtensions
    {
        /// <summary>
        /// 使用 lambda 表达式过滤行。
        /// <code>
        /// .Where(row => row.Get&lt;double&gt;("Revenue") > 1000)
        /// </code>
        /// </summary>
        public static ITabularQuery Where(this ITabularQuery query, Func<QueryRow, bool> predicate)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            // 包装为字典级别的谓词，委托给 WhereCustom
            return query.WhereCustom(dict => predicate(new QueryRow(dict)));
        }

        /// <summary>
        /// 使用自定义字典谓词过滤行（内部使用）。
        /// 各 ITabularQuery 实现可以 override 此方法以获得更好性能。
        /// </summary>
        internal static ITabularQuery WhereCustom(this ITabularQuery query, Func<Dictionary<string, object>, bool> predicate)
        {
            // 通过包装查询实现
            return new LambdaFilteredQuery(query, predicate);
        }

        /// <summary>
        /// 终结方法：执行查询并返回结果列表。
        /// 语义等价于 ToDictionaries()，但名称更符合 LINQ 风格。
        /// </summary>
        public static List<Dictionary<string, object>> Execute(this ITabularQuery query)
            => query.ToDictionaries();

        /// <summary>
        /// 终结方法：返回匹配行的 QueryRow 列表（类型安全）。
        /// </summary>
        public static List<QueryRow> ToRows(this ITabularQuery query)
        {
            var dicts = query.ToDictionaries();
            var rows = new List<QueryRow>(dicts.Count);
            foreach (var d in dicts)
                rows.Add(new QueryRow(d));
            return rows;
        }

        /// <summary>
        /// 终结方法：异步返回匹配行的 QueryRow 列表（类型安全）。
        /// </summary>
        public static async Task<List<QueryRow>> ToRowsAsync(this ITabularQuery query, CancellationToken ct = default)
        {
            var dicts = await query.ExecuteAsync(ct).ConfigureAwait(false);
            var rows = new List<QueryRow>(dicts.Count);
            foreach (var d in dicts)
                rows.Add(new QueryRow(d));
            return rows;
        }
    }

    /// <summary>
    /// 包装查询 — 在现有查询之上叠加 lambda 过滤器。
    /// 委托所有非过滤方法给底层查询，在执行时叠加过滤。
    /// </summary>
    internal sealed class LambdaFilteredQuery : ITabularQuery
    {
        private readonly ITabularQuery _inner;
        private readonly Func<Dictionary<string, object>, bool> _predicate;

        public LambdaFilteredQuery(ITabularQuery inner, Func<Dictionary<string, object>, bool> predicate)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        // ── 过滤：继续叠加 lambda ──

        public ITabularQuery WhereEquals(string column, object value)
            => new LambdaFilteredQuery(_inner.WhereEquals(column, value), _predicate);

        public ITabularQuery WhereNotEquals(string column, object value)
            => new LambdaFilteredQuery(_inner.WhereNotEquals(column, value), _predicate);

        public ITabularQuery WhereGreaterThan(string column, double value)
            => new LambdaFilteredQuery(_inner.WhereGreaterThan(column, value), _predicate);

        public ITabularQuery WhereGreaterThanOrEqual(string column, double value)
            => new LambdaFilteredQuery(_inner.WhereGreaterThanOrEqual(column, value), _predicate);

        public ITabularQuery WhereLessThan(string column, double value)
            => new LambdaFilteredQuery(_inner.WhereLessThan(column, value), _predicate);

        public ITabularQuery WhereLessThanOrEqual(string column, double value)
            => new LambdaFilteredQuery(_inner.WhereLessThanOrEqual(column, value), _predicate);

        public ITabularQuery WhereBetween(string column, double min, double max)
            => new LambdaFilteredQuery(_inner.WhereBetween(column, min, max), _predicate);

        public ITabularQuery WhereContains(string column, string value)
            => new LambdaFilteredQuery(_inner.WhereContains(column, value), _predicate);

        public ITabularQuery WhereStartsWith(string column, string value)
            => new LambdaFilteredQuery(_inner.WhereStartsWith(column, value), _predicate);

        public ITabularQuery WhereIn<T>(string column, IEnumerable<T> values)
            => new LambdaFilteredQuery(_inner.WhereIn(column, values), _predicate);

        public ITabularQuery WhereIsNull(string column)
            => new LambdaFilteredQuery(_inner.WhereIsNull(column), _predicate);

        public ITabularQuery WhereIsNotNull(string column)
            => new LambdaFilteredQuery(_inner.WhereIsNotNull(column), _predicate);

        public ITabularQuery Where(string column, QueryOp op, object value)
            => new LambdaFilteredQuery(_inner.Where(column, op, value), _predicate);

        // ── 排序/分页/选择：委托给 inner ──

        public ITabularQuery OrderBy(string column) => _inner.OrderBy(column);
        public ITabularQuery OrderByDescending(string column) => _inner.OrderByDescending(column);
        public ITabularQuery OrderBy(string column, SortDirection direction) => _inner.OrderBy(column, direction);
        public ITabularQuery Skip(int count) => _inner.Skip(count);
        public ITabularQuery Limit(int count) => _inner.Limit(count);
        public ITabularQuery Page(int pageNumber, int pageSize) => _inner.Page(pageNumber, pageSize);
        public ITabularQuery Select(params string[] columns) => _inner.Select(columns);

        // ── 执行：inner 执行后再过滤 ──

        public int[] ToRowIndices()
        {
            // 需要获取行数据来过滤，用 ToDictionaries 的索引
            var all = _inner.ToDictionaries();
            var indices = new List<int>();
            for (int i = 0; i < all.Count; i++)
            {
                if (_predicate(all[i]))
                    indices.Add(i);
            }
            return indices.ToArray();
        }

        public List<Dictionary<string, object>> ToDictionaries()
        {
            var all = _inner.ToDictionaries();
            return all.FindAll(d => _predicate(d));
        }

        public int Count()
        {
            var all = _inner.ToDictionaries();
            int count = 0;
            foreach (var d in all)
            {
                if (_predicate(d)) count++;
            }
            return count;
        }

        public bool Any()
        {
            var all = _inner.ToDictionaries();
            foreach (var d in all)
            {
                if (_predicate(d)) return true;
            }
            return false;
        }

        public Dictionary<string, object> FirstOrDefault()
        {
            var all = _inner.ToDictionaries();
            foreach (var d in all)
            {
                if (_predicate(d)) return d;
            }
            return null;
        }

        // ── 聚合：inner 聚合后再过滤（效率低但正确） ──

        public double Sum(string column)
        {
            double sum = 0;
            foreach (var d in _inner.ToDictionaries())
            {
                if (_predicate(d) && d.TryGetValue(column, out var v) && v != null)
                    sum += Convert.ToDouble(v);
            }
            return sum;
        }

        public double Average(string column)
        {
            double sum = 0; int count = 0;
            foreach (var d in _inner.ToDictionaries())
            {
                if (_predicate(d) && d.TryGetValue(column, out var v) && v != null)
                {
                    sum += Convert.ToDouble(v);
                    count++;
                }
            }
            return count > 0 ? sum / count : 0;
        }

        public double Max(string column)
        {
            double max = double.MinValue; bool found = false;
            foreach (var d in _inner.ToDictionaries())
            {
                if (_predicate(d) && d.TryGetValue(column, out var v) && v != null)
                {
                    var val = Convert.ToDouble(v);
                    if (!found || val > max) { max = val; found = true; }
                }
            }
            return found ? max : 0;
        }

        public double Min(string column)
        {
            double min = double.MaxValue; bool found = false;
            foreach (var d in _inner.ToDictionaries())
            {
                if (_predicate(d) && d.TryGetValue(column, out var v) && v != null)
                {
                    var val = Convert.ToDouble(v);
                    if (!found || val < min) { min = val; found = true; }
                }
            }
            return found ? min : 0;
        }

        // ── 异步执行 ──

        public async Task<List<Dictionary<string, object>>> ExecuteAsync(CancellationToken ct = default)
        {
            var all = await _inner.ExecuteAsync(ct).ConfigureAwait(false);
            return all.FindAll(d => _predicate(d));
        }

        public async Task<int> CountAsync(CancellationToken ct = default)
        {
            var all = await _inner.ExecuteAsync(ct).ConfigureAwait(false);
            int count = 0;
            foreach (var d in all)
            {
                ct.ThrowIfCancellationRequested();
                if (_predicate(d)) count++;
            }
            return count;
        }

        public async Task<Dictionary<string, object>> FirstOrDefaultAsync(CancellationToken ct = default)
        {
            var all = await _inner.ExecuteAsync(ct).ConfigureAwait(false);
            foreach (var d in all)
            {
                ct.ThrowIfCancellationRequested();
                if (_predicate(d)) return d;
            }
            return null;
        }
    }
}
