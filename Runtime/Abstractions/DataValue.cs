using System;

namespace AroAro.DataCore
{
    /// <summary>
    /// 后端无关的标量值包装器，隔离 LiteDB.BsonValue 对公共 API 的泄漏
    /// </summary>
    public readonly struct DataValue
    {
        private readonly object _value;

        public DataValue(object value) => _value = value;

        /// <summary>
        /// 值是否为 null
        /// </summary>
        public bool IsNull => _value == null;

        /// <summary>
        /// 获取 Int32 值
        /// </summary>
        public int AsInt32 => _value is int i ? i : Convert.ToInt32(_value ?? 0);

        /// <summary>
        /// 获取 Int64 值
        /// </summary>
        public long AsInt64 => _value is long l ? l : Convert.ToInt64(_value ?? 0L);

        /// <summary>
        /// 获取 Double 值
        /// </summary>
        public double AsDouble => _value is double d ? d : Convert.ToDouble(_value ?? 0.0);

        /// <summary>
        /// 获取字符串值
        /// </summary>
        public string AsString => _value?.ToString() ?? string.Empty;

        /// <summary>
        /// 获取布尔值
        /// </summary>
        public bool AsBoolean => _value is bool b && b;

        /// <summary>
        /// 空值实例
        /// </summary>
        public static DataValue Null => new DataValue(null);
    }
}
