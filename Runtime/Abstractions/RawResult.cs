using System;
using LiteDB;

namespace AroAro.DataCore
{
    /// <summary>
    /// 原生 LiteDB 查询执行结果
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
        public BsonValue ScalarValue { get; }
        
        /// <summary>
        /// 是否有表格数据
        /// </summary>
        public bool HasData => Data != null && Data.RowCount > 0;
        
        /// <summary>
        /// 快捷获取整数结果
        /// </summary>
        public int AsInt32 => ScalarValue?.AsInt32 ?? 0;
        
        /// <summary>
        /// 快捷获取长整数结果
        /// </summary>
        public long AsInt64 => ScalarValue?.AsInt64 ?? 0;
        
        /// <summary>
        /// 快捷获取浮点结果
        /// </summary>
        public double AsDouble => ScalarValue?.AsDouble ?? 0;
        
        /// <summary>
        /// 快捷获取字符串结果
        /// </summary>
        public string AsString => ScalarValue?.AsString ?? string.Empty;
        
        /// <summary>
        /// 快捷获取布尔结果
        /// </summary>
        public bool AsBoolean => ScalarValue?.AsBoolean ?? false;
        
        /// <summary>
        /// 创建表格数据结果
        /// </summary>
        public RawResult(ITabularDataset data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            ScalarValue = null;
        }
        
        /// <summary>
        /// 创建标量结果
        /// </summary>
        public RawResult(BsonValue scalarValue)
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
            ScalarValue = null;
        }
    }
}