using System;
using System.Collections.Generic;
using NumSharp;

namespace AroAro.DataCore
{
    /// <summary>
    /// 表格数据集接口 - 提供类似 DataFrame 的操作
    /// </summary>
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
        /// 获取数值列数据
        /// </summary>
        NDArray GetNumericColumn(string name);

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
