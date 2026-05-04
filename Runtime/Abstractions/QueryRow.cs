using System;
using System.Collections.Generic;

namespace AroAro.DataCore
{
    /// <summary>
    /// 类型安全的查询行包装器。
    /// 在 lambda Where 中使用，提供 Get&lt;T&gt;() 泛型访问。
    /// </summary>
    /// <example>
    /// <code>
    /// query.Where(row => row.Get&lt;float&gt;("Revenue") > 1000f)
    /// </code>
    /// </example>
    public readonly struct QueryRow
    {
        private readonly Dictionary<string, object> _data;

        public QueryRow(Dictionary<string, object> data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// 获取指定列的值，并转换为类型 T。
        /// </summary>
        /// <typeparam name="T">目标类型（double, float, int, long, string, bool, DateTime）</typeparam>
        /// <param name="column">列名</param>
        /// <returns>转换后的值</returns>
        /// <exception cref="KeyNotFoundException">列不存在</exception>
        /// <exception cref="InvalidCastException">类型转换失败</exception>
        public T Get<T>(string column)
        {
            if (_data == null)
                throw new InvalidOperationException("QueryRow has no data");

            if (!_data.TryGetValue(column, out var value))
                throw new KeyNotFoundException($"Column '{column}' not found in row");

            if (value == null)
            {
                var defaultVal = default(T);
                if (defaultVal != null)
                    throw new InvalidCastException($"Column '{column}' is null, cannot convert to {typeof(T).Name}");
                return default;
            }

            if (value is T typed)
                return typed;

            // 数值类型之间的转换
            var targetType = typeof(T);
            if (targetType == typeof(double))
                return (T)(object)Convert.ToDouble(value);
            if (targetType == typeof(float))
                return (T)(object)Convert.ToSingle(value);
            if (targetType == typeof(int))
                return (T)(object)Convert.ToInt32(value);
            if (targetType == typeof(long))
                return (T)(object)Convert.ToInt64(value);
            if (targetType == typeof(bool))
                return (T)(object)Convert.ToBoolean(value);
            if (targetType == typeof(string))
                return (T)(object)value.ToString();
            if (targetType == typeof(DateTime))
                return (T)(object)Convert.ToDateTime(value);

            return (T)Convert.ChangeType(value, targetType);
        }

        /// <summary>
        /// 尝试获取指定列的值，不抛异常。
        /// </summary>
        public bool TryGet<T>(string column, out T value)
        {
            value = default;
            if (_data == null || !_data.TryGetValue(column, out var raw) || raw == null)
                return false;

            try
            {
                value = Get<T>(column);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取原始 object 值。
        /// </summary>
        public object GetRaw(string column)
        {
            if (_data == null)
                throw new InvalidOperationException("QueryRow has no data");

            if (!_data.TryGetValue(column, out var value))
                throw new KeyNotFoundException($"Column '{column}' not found in row");

            return value;
        }

        /// <summary>
        /// 检查列是否存在且非空。
        /// </summary>
        public bool Has(string column)
        {
            return _data != null && _data.TryGetValue(column, out var v) && v != null;
        }

        /// <summary>
        /// 检查列值是否为 null。
        /// </summary>
        public bool IsNull(string column)
        {
            return _data == null || !_data.TryGetValue(column, out var v) || v == null;
        }

        /// <summary>
        /// 获取所有列名。
        /// </summary>
        public IEnumerable<string> Columns => (IEnumerable<string>)_data?.Keys ?? Array.Empty<string>();

        /// <summary>
        /// 底层数据（高级场景）。
        /// </summary>
        public Dictionary<string, object> Raw => _data;
    }
}
