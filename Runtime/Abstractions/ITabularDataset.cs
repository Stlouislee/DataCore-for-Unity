using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NumSharp;

namespace AroAro.DataCore
{
    /// <summary>
    /// 表格数据集接口 - 提供类似 DataFrame 的操作
    /// </summary>
    /// <remarks>
    /// <para>⚠️ Performance note: All synchronous operations execute on the calling thread. For large
    /// datasets (&gt;10000 rows), prefer the async variants (e.g. <see cref="AddRowsAsync"/>,
    /// <see cref="ExecuteRawAsync"/>, <see cref="ExportToCsvAsync"/>, <see cref="ImportFromCsvAsync"/>)
    /// to avoid blocking the Unity main thread:</para>
    /// <code>
    /// var csv = await dataset.ExportToCsvAsync();
    /// var result = await dataset.ExecuteRawAsync("SELECT * FROM data", Array.Empty&lt;object&gt;());
    /// </code>
    /// </remarks>
    public interface ITabularDataset : IDataSet
    {
        #region 属性

        /// <summary>
        /// 行数
        /// </summary>
        int RowCount { get; }

        /// <summary>
        /// 列数
        /// </summary>
        int ColumnCount { get; }

        /// <summary>
        /// 列名集合
        /// </summary>
        IReadOnlyCollection<string> ColumnNames { get; }

        #endregion

        #region 列操作

        /// <summary>
        /// 添加数值列
        /// </summary>
        void AddNumericColumn(string name, double[] data);

        /// <summary>
        /// 添加数值列 (NDArray)
        /// </summary>
        [Obsolete("Use AddNumericColumn(name, double[]) instead. Will be removed in v1.0.")]
        void AddNumericColumn(string name, NDArray data);

        /// <summary>
        /// 添加字符串列
        /// </summary>
        void AddStringColumn(string name, string[] data);

        /// <summary>
        /// 移除列
        /// </summary>
        bool RemoveColumn(string name);

        /// <summary>
        /// 检查列是否存在
        /// </summary>
        bool HasColumn(string name);

        /// <summary>
        /// 获取数值列数据 (NDArray)
        /// </summary>
        [Obsolete("Use GetNumericColumnRaw(name) instead. Will be removed in v1.0.")]
        NDArray GetNumericColumn(string name);

        /// <summary>
        /// 获取原始 double[] 数组（无克隆，无包装）
        /// </summary>
        double[] GetNumericColumnRaw(string name);

        /// <summary>
        /// 获取字符串列数据
        /// </summary>
        string[] GetStringColumn(string name);

        /// <summary>
        /// 获取列类型
        /// </summary>
        ColumnType GetColumnType(string name);

        #endregion

        #region 行操作

        /// <summary>
        /// 添加行
        /// </summary>
        void AddRow(IDictionary<string, object> values);

        /// <summary>
        /// 批量添加行
        /// </summary>
        int AddRows(IEnumerable<IDictionary<string, object>> rows);

        /// <summary>
        /// 更新行
        /// </summary>
        bool UpdateRow(int rowIndex, IDictionary<string, object> values);

        /// <summary>
        /// 删除行
        /// </summary>
        bool DeleteRow(int rowIndex);

        /// <summary>
        /// 获取行数据
        /// </summary>
        Dictionary<string, object> GetRow(int rowIndex);

        /// <summary>
        /// 获取多行数据
        /// </summary>
        IEnumerable<Dictionary<string, object>> GetRows(int startIndex, int count);

        /// <summary>
        /// 清空所有行数据
        /// </summary>
        int Clear();

        /// <summary>
        /// Compact the dataset by removing gaps left by deleted rows and re-indexing.
        /// </summary>
        void Compact();

        #endregion

        #region 查询

        /// <summary>
        /// 创建查询构建器
        /// </summary>
        ITabularQuery Query();

        /// <summary>
        /// 简单过滤查询
        /// </summary>
        int[] Where(string column, QueryOp op, object value);

        #endregion

        #region CSV 导入导出

        /// <summary>
        /// 从 CSV 字符串导入数据
        /// </summary>
        void ImportFromCsv(string csvContent, bool hasHeader = true, char delimiter = ',');

        /// <summary>
        /// 导出为 CSV 字符串
        /// </summary>
        string ExportToCsv(char delimiter = ',', bool includeHeader = true);

        #endregion

        #region 统计函数

        /// <summary>
        /// 计算数值列的和
        /// </summary>
        double Sum(string column);

        /// <summary>
        /// 计算数值列的平均值
        /// </summary>
        double Mean(string column);

        /// <summary>
        /// 获取数值列的最大值
        /// </summary>
        double Max(string column);

        /// <summary>
        /// 获取数值列的最小值
        /// </summary>
        double Min(string column);

        /// <summary>
        /// 计算数值列的标准差
        /// </summary>
        double Std(string column);

        #endregion

        #region 索引

        /// <summary>
        /// 为列创建索引以加速查询
        /// </summary>
        void CreateIndex(string columnName);

        #endregion

        #region 原生查询

        /// <summary>
        /// 执行原生 LiteDB SQL-like 命令
        /// </summary>
        /// <param name="sql">SQL-like 查询语句</param>
        /// <param name="args">参数值（@0, @1...）</param>
        /// <returns>执行结果</returns>
        RawResult ExecuteRaw(string sql, params object[] args);

        #endregion

        #region 异步操作

        /// <summary>
        /// 异步添加行
        /// </summary>
        Task AddRowAsync(IDictionary<string, object> values, CancellationToken ct = default);

        /// <summary>
        /// 异步批量添加行
        /// </summary>
        Task<int> AddRowsAsync(IEnumerable<IDictionary<string, object>> rows, CancellationToken ct = default);

        /// <summary>
        /// 异步添加数值列
        /// </summary>
        Task AddNumericColumnAsync(string name, double[] data, CancellationToken ct = default);

        /// <summary>
        /// 异步添加字符串列
        /// </summary>
        Task AddStringColumnAsync(string name, string[] data, CancellationToken ct = default);

        /// <summary>
        /// 异步清空所有行数据
        /// </summary>
        Task<int> ClearAsync(CancellationToken ct = default);

        /// <summary>
        /// 异步执行原生 LiteDB SQL-like 命令（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: This operation runs on a background thread via Task.Run to avoid
        /// blocking the Unity main thread. Recommended for large datasets where query execution
        /// may take significant time.</para>
        /// </remarks>
        /// <param name="sql">SQL-like 查询语句</param>
        /// <param name="args">参数值（@0, @1...）</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>执行结果</returns>
        Task<RawResult> ExecuteRawAsync(string sql, object[] args, CancellationToken ct = default);

        /// <summary>
        /// 异步导出为 CSV 字符串（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: This operation runs on a background thread via Task.Run to avoid
        /// blocking the Unity main thread. Recommended for large datasets (&gt;10000 rows) where
        /// CSV serialization may be slow.</para>
        /// </remarks>
        /// <param name="delimiter">分隔符</param>
        /// <param name="includeHeader">是否包含表头</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>CSV 字符串</returns>
        Task<string> ExportToCsvAsync(char delimiter = ',', bool includeHeader = true, CancellationToken ct = default);

        /// <summary>
        /// 异步从 CSV 字符串导入数据（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: This operation runs on a background thread via Task.Run to avoid
        /// blocking the Unity main thread. Recommended for large CSV files where parsing
        /// may take significant time.</para>
        /// </remarks>
        /// <param name="csvContent">CSV 内容</param>
        /// <param name="hasHeader">是否包含表头</param>
        /// <param name="delimiter">分隔符</param>
        /// <param name="ct">取消令牌</param>
        Task ImportFromCsvAsync(string csvContent, bool hasHeader = true, char delimiter = ',', CancellationToken ct = default);

        #endregion
    }

    /// <summary>
    /// 列类型
    /// </summary>
    public enum ColumnType
    {
        /// <summary>
        /// 数值类型
        /// </summary>
        Numeric = 1,

        /// <summary>
        /// 字符串类型
        /// </summary>
        String = 2,

        /// <summary>
        /// 布尔类型
        /// </summary>
        Boolean = 3,

        /// <summary>
        /// 日期时间类型
        /// </summary>
        DateTime = 4,

        /// <summary>
        /// 未知类型
        /// </summary>
        Unknown = 0
    }
}
