using System;
using System.Collections.Generic;

namespace AroAro.DataCore
{
    /// <summary>
    /// 表格查询接口 - 提供流式查询 API
    /// </summary>
    public interface ITabularQuery
    {
        #region 过滤条件

        /// <summary>
        /// 添加相等条件
        /// </summary>
        ITabularQuery WhereEquals(string column, object value);

        /// <summary>
        /// 添加不等条件
        /// </summary>
        ITabularQuery WhereNotEquals(string column, object value);

        /// <summary>
        /// 添加大于条件
        /// </summary>
        ITabularQuery WhereGreaterThan(string column, double value);

        /// <summary>
        /// 添加大于等于条件
        /// </summary>
        ITabularQuery WhereGreaterThanOrEqual(string column, double value);

        /// <summary>
        /// 添加小于条件
        /// </summary>
        ITabularQuery WhereLessThan(string column, double value);

        /// <summary>
        /// 添加小于等于条件
        /// </summary>
        ITabularQuery WhereLessThanOrEqual(string column, double value);

        /// <summary>
        /// 添加范围条件
        /// </summary>
        ITabularQuery WhereBetween(string column, double min, double max);

        /// <summary>
        /// 添加包含条件（字符串）
        /// </summary>
        ITabularQuery WhereContains(string column, string value);

        /// <summary>
        /// 添加开头匹配条件（字符串）
        /// </summary>
        ITabularQuery WhereStartsWith(string column, string value);

        /// <summary>
        /// 添加 IN 条件
        /// </summary>
        ITabularQuery WhereIn<T>(string column, IEnumerable<T> values);

        /// <summary>
        /// 添加空值条件
        /// </summary>
        ITabularQuery WhereIsNull(string column);

        /// <summary>
        /// 添加非空值条件
        /// </summary>
        ITabularQuery WhereIsNotNull(string column);

        /// <summary>
        /// 使用 QueryOp 枚举添加条件
        /// </summary>
        ITabularQuery Where(string column, QueryOp op, object value);

        #endregion

        #region 排序

        /// <summary>
        /// 按列升序排列
        /// </summary>
        ITabularQuery OrderBy(string column);

        /// <summary>
        /// 按列降序排列
        /// </summary>
        ITabularQuery OrderByDescending(string column);

        #endregion

        #region 分页

        /// <summary>
        /// 跳过指定数量的行
        /// </summary>
        ITabularQuery Skip(int count);

        /// <summary>
        /// 限制返回的行数
        /// </summary>
        ITabularQuery Limit(int count);

        /// <summary>
        /// 分页
        /// </summary>
        ITabularQuery Page(int pageNumber, int pageSize);

        #endregion

        #region 列选择

        /// <summary>
        /// 选择特定列
        /// </summary>
        ITabularQuery Select(params string[] columns);

        #endregion

        #region 执行查询

        /// <summary>
        /// 执行查询并返回行索引
        /// </summary>
        int[] ToRowIndices();

        /// <summary>
        /// 执行查询并返回字典列表
        /// </summary>
        List<Dictionary<string, object>> ToDictionaries();

        /// <summary>
        /// 返回匹配的行数
        /// </summary>
        int Count();

        /// <summary>
        /// 检查是否存在匹配的行
        /// </summary>
        bool Any();

        /// <summary>
        /// 返回第一行或 null
        /// </summary>
        Dictionary<string, object> FirstOrDefault();

        #endregion

        #region 聚合函数

        /// <summary>
        /// 计算数值列的和
        /// </summary>
        double Sum(string column);

        /// <summary>
        /// 计算数值列的平均值
        /// </summary>
        double Average(string column);

        /// <summary>
        /// 获取数值列的最大值
        /// </summary>
        double Max(string column);

        /// <summary>
        /// 获取数值列的最小值
        /// </summary>
        double Min(string column);

        #endregion
    }

    /// <summary>
    /// 查询操作符
    /// </summary>
    public enum QueryOp
    {
        /// <summary>
        /// 等于
        /// </summary>
        Eq = 1,

        /// <summary>
        /// 不等于
        /// </summary>
        Ne = 2,

        /// <summary>
        /// 大于
        /// </summary>
        Gt = 3,

        /// <summary>
        /// 大于等于
        /// </summary>
        Ge = 4,

        /// <summary>
        /// 小于
        /// </summary>
        Lt = 5,

        /// <summary>
        /// 小于等于
        /// </summary>
        Le = 6,

        /// <summary>
        /// 包含（字符串）
        /// </summary>
        Contains = 7,

        /// <summary>
        /// 开头匹配（字符串）
        /// </summary>
        StartsWith = 8,

        /// <summary>
        /// 结尾匹配（字符串）
        /// </summary>
        EndsWith = 9
    }
}
