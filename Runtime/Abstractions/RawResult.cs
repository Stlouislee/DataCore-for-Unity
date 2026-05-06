using System;

namespace AroAro.DataCore
{
    /// <summary>
    /// 原生查询执行结果（后端无关）
    /// </summary>
    public class RawResult
    {
        /// <summary>
        /// 结果数据（SELECT 返回的行）
        /// </summary>
        public ITabularDataset Data { get; }

        /// <summary>
        /// 标量结果（COUNT/UPDATE/DELETE 返回的值）
        /// </summary>
        public DataValue ScalarValue { get; }

        /// <summary>
        /// 是否有表格数据
        /// </summary>
        public bool HasData => Data != null && Data.RowCount > 0;

        /// <summary>
        /// 快捷获取整数结果
        /// </summary>
        public int AsInt32 => ScalarValue.AsInt32;

        /// <summary>
        /// 快捷获取长整数结果
        /// </summary>
        public long AsInt64 => ScalarValue.AsInt64;

        /// <summary>
        /// 快捷获取浮点结果
        /// </summary>
        public double AsDouble => ScalarValue.AsDouble;

        /// <summary>
        /// 快捷获取字符串结果
        /// </summary>
        public string AsString => ScalarValue.AsString;

        /// <summary>
        /// 快捷获取布尔结果
        /// </summary>
        public bool AsBoolean => ScalarValue.AsBoolean;

        /// <summary>
        /// 创建表格数据结果
        /// </summary>
        public RawResult(ITabularDataset data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            ScalarValue = DataValue.Null;
        }

        /// <summary>
        /// 创建标量结果
        /// </summary>
        public RawResult(DataValue scalarValue)
        {
            Data = null;
            ScalarValue = scalarValue;
        }

        /// <summary>
        /// 创建空结果
        /// </summary>
        public RawResult()
        {
            Data = null;
            ScalarValue = DataValue.Null;
        }
    }
}
