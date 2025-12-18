using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// Session DataFrame查询构建器，提供流畅的查询API
    /// </summary>
    public class SessionDataFrameQueryBuilder
    {
        private readonly ISession _session;
        private readonly string _sourceName;
        private readonly List<Func<DataFrame, DataFrame>> _operations = new();
        private string _queryDescription = "DataFrame Query";

        public SessionDataFrameQueryBuilder(ISession session, string sourceName)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _sourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
        }

        /// <summary>
        /// 设置查询描述
        /// </summary>
        public SessionDataFrameQueryBuilder WithDescription(string description)
        {
            _queryDescription = description ?? "DataFrame Query";
            return this;
        }

        /// <summary>
        /// 数值列过滤
        /// </summary>
        public SessionDataFrameQueryBuilder Where(string columnName, ComparisonOp op, double value)
        {
            _operations.Add(df =>
            {
                if (df.Columns.IndexOf(columnName) == -1)
                    throw new ArgumentException($"Column '{columnName}' not found in DataFrame");

                var column = df.Columns[columnName];
                if (!(column is PrimitiveDataFrameColumn<double>))
                    throw new InvalidOperationException($"Column '{columnName}' is not a numeric column");

                var filterColumn = column as PrimitiveDataFrameColumn<double>;
                
                // 创建布尔掩码数组
                var mask = new bool[filterColumn.Length];
                for (int i = 0; i < filterColumn.Length; i++)
                {
                    var cellValue = filterColumn[i];
                    if (cellValue.HasValue)
                    {
                        switch (op)
                        {
                            case ComparisonOp.Gt:
                                mask[i] = cellValue.Value > value;
                                break;
                            case ComparisonOp.Ge:
                                mask[i] = cellValue.Value >= value;
                                break;
                            case ComparisonOp.Lt:
                                mask[i] = cellValue.Value < value;
                                break;
                            case ComparisonOp.Le:
                                mask[i] = cellValue.Value <= value;
                                break;
                            case ComparisonOp.Eq:
                                mask[i] = cellValue.Value == value;
                                break;
                            case ComparisonOp.Ne:
                                mask[i] = cellValue.Value != value;
                                break;
                            default:
                                throw new ArgumentException($"Unsupported comparison operator: {op}");
                        }
                    }
                    else
                    {
                        mask[i] = false; // 空值不匹配
                    }
                }
                
                // 创建布尔列并过滤
                var maskColumn = new PrimitiveDataFrameColumn<bool>("mask", mask);
                return df.Filter(maskColumn);
            });

            return this;
        }

        /// <summary>
        /// 字符串列过滤
        /// </summary>
        public SessionDataFrameQueryBuilder Where(string columnName, ComparisonOp op, string value)
        {
            _operations.Add(df =>
            {
                if (df.Columns.IndexOf(columnName) == -1)
                    throw new ArgumentException($"Column '{columnName}' not found in DataFrame");

                var column = df.Columns[columnName];
                if (!(column is StringDataFrameColumn))
                    throw new InvalidOperationException($"Column '{columnName}' is not a string column");

                var filterColumn = column as StringDataFrameColumn;
                
                // 创建布尔掩码数组
                var mask = new bool[filterColumn.Length];
                for (int i = 0; i < filterColumn.Length; i++)
                {
                    var cellValue = filterColumn[i];
                    if (cellValue != null)
                    {
                        switch (op)
                        {
                            case ComparisonOp.Eq:
                                mask[i] = cellValue == value;
                                break;
                            case ComparisonOp.Ne:
                                mask[i] = cellValue != value;
                                break;
                            case ComparisonOp.Contains:
                                mask[i] = cellValue.Contains(value);
                                break;
                            case ComparisonOp.StartsWith:
                                mask[i] = cellValue.StartsWith(value);
                                break;
                            case ComparisonOp.EndsWith:
                                mask[i] = cellValue.EndsWith(value);
                                break;
                            default:
                                throw new ArgumentException($"Unsupported string comparison operator: {op}");
                        }
                    }
                    else
                    {
                        mask[i] = false; // 空值不匹配
                    }
                }
                
                // 创建布尔列并过滤
                var maskColumn = new PrimitiveDataFrameColumn<bool>("mask", mask);
                return df.Filter(maskColumn);
            });

            return this;
        }

        /// <summary>
        /// 布尔列过滤
        /// </summary>
        public SessionDataFrameQueryBuilder Where(string columnName, bool value)
        {
            _operations.Add(df =>
            {
                if (df.Columns.IndexOf(columnName) == -1)
                    throw new ArgumentException($"Column '{columnName}' not found in DataFrame");

                var column = df.Columns[columnName];
                if (!(column is BooleanDataFrameColumn))
                    throw new InvalidOperationException($"Column '{columnName}' is not a boolean column");

                var filterColumn = column as BooleanDataFrameColumn;
                
                // 创建布尔掩码数组
                var mask = new bool[filterColumn.Length];
                for (int i = 0; i < filterColumn.Length; i++)
                {
                    var cellValue = filterColumn[i];
                    if (cellValue.HasValue)
                    {
                        mask[i] = cellValue.Value == value;
                    }
                    else
                    {
                        mask[i] = false; // 空值不匹配
                    }
                }
                
                // 创建布尔列并过滤
                var maskColumn = new PrimitiveDataFrameColumn<bool>("mask", mask);
                return df.Filter(maskColumn);
            });

            return this;
        }

        /// <summary>
        /// 选择特定列
        /// </summary>
        public SessionDataFrameQueryBuilder Select(params string[] columns)
        {
            _operations.Add(df =>
            {
                foreach (var columnName in columns)
                {
                    if (df.Columns.IndexOf(columnName) == -1)
                        throw new ArgumentException($"Column '{columnName}' not found in DataFrame");
                }
                // 选择特定列 - 创建新的DataFrame包含指定的列
                var selectedColumns = columns.Select(colName => df.Columns[colName]).ToArray();
                return new DataFrame(selectedColumns);
            });

            return this;
        }

        /// <summary>
        /// 排序
        /// </summary>
        public SessionDataFrameQueryBuilder OrderBy(string columnName, bool ascending = true)
        {
            _operations.Add(df =>
            {
                if (df.Columns.IndexOf(columnName) == -1)
                    throw new ArgumentException($"Column '{columnName}' not found in DataFrame");

                var column = df.Columns[columnName];
                return ascending ? df.OrderBy(columnName) : df.OrderByDescending(columnName);
            });

            return this;
        }

        /// <summary>
        /// 限制返回行数
        /// </summary>
        public SessionDataFrameQueryBuilder Limit(int count)
        {
            if (count <= 0)
                throw new ArgumentException("Limit count must be positive", nameof(count));

            _operations.Add(df => df.Head(count));
            return this;
        }

        /// <summary>
        /// 跳过指定行数
        /// </summary>
        public SessionDataFrameQueryBuilder Offset(int count)
        {
            if (count < 0)
                throw new ArgumentException("Offset count cannot be negative", nameof(count));

            _operations.Add(df => df.Tail(Math.Max(0, (int)(df.Rows.Count - count))));
            return this;
        }

        /// <summary>
        /// 分组聚合
        /// </summary>
        public SessionDataFrameQueryBuilder GroupBy(string groupColumn, params (string column, AggregateFunction function)[] aggregates)
        {
            _operations.Add(df =>
            {
                if (df.Columns.IndexOf(groupColumn) == -1)
                    throw new ArgumentException($"Group column '{groupColumn}' not found in DataFrame");

                // 使用内置的GroupBy API
                var groupKeyColumn = df.Columns[groupColumn];
                var groupBy = df.GroupBy(groupColumn);
                
                // 为每个聚合列准备列名数组
                var aggregateColumns = aggregates.Select(a => a.column).ToArray();
                
                // 根据聚合函数调用相应的方法
                DataFrame resultDf = null;
                
                foreach (var (aggColumn, function) in aggregates)
                {
                    if (df.Columns.IndexOf(aggColumn) == -1)
                        throw new ArgumentException($"Aggregate column '{aggColumn}' not found in DataFrame");

                    var numericColumn = df.Columns[aggColumn] as PrimitiveDataFrameColumn<double>;
                    if (numericColumn == null)
                        throw new InvalidOperationException($"Column '{aggColumn}' is not numeric, cannot aggregate");

                    switch (function)
                    {
                        case AggregateFunction.Sum:
                            resultDf = groupBy.Sum(aggColumn);
                            break;
                        case AggregateFunction.Average:
                            resultDf = groupBy.Mean(aggColumn);
                            break;
                        case AggregateFunction.Min:
                            resultDf = groupBy.Min(aggColumn);
                            break;
                        case AggregateFunction.Max:
                            resultDf = groupBy.Max(aggColumn);
                            break;
                        case AggregateFunction.Count:
                            resultDf = groupBy.Count(aggColumn);
                            break;
                    }
                    
                    // 重命名聚合列以反映聚合函数
                    if (resultDf != null)
                    {
                        var originalColumnName = aggColumn;
                        var aggregatedColumnName = $"{aggColumn}_{function}";
                        
                        // 查找并重命名聚合列
                        for (int i = 0; i < resultDf.Columns.Count; i++)
                        {
                            if (resultDf.Columns[i].Name == originalColumnName)
                            {
                                resultDf.Columns[i].SetName(aggregatedColumnName);
                                break;
                            }
                        }
                    }
                }

                // 将列名数组转换为 DataFrameColumn 数组，然后执行 Count
                var aggregateDfColumns = aggregateColumns.Select(name => df.Columns[name]).ToArray();
                return resultDf ?? groupBy.Count(aggregateColumns); // 默认返回计数
            });

            return this;
        }

        /// <summary>
        /// 执行查询并返回结果数据集
        /// </summary>
        public IDataSet Execute(string resultName)
        {
            if (string.IsNullOrWhiteSpace(resultName))
                throw new ArgumentException("Result dataset name cannot be null or empty", nameof(resultName));

            // 获取Session的具体实现
            var concreteSession = _session as Session;
            if (concreteSession == null)
                throw new InvalidOperationException("Session must be concrete implementation");

            // 执行查询
            return concreteSession.ExecuteDataFrameQuery(_sourceName, df =>
            {
                var resultDf = df;
                foreach (var operation in _operations)
                {
                    resultDf = operation(resultDf);
                }
                return resultDf;
            }, resultName);
        }

        /// <summary>
        /// 执行查询并返回DataFrame（不保存到Session）
        /// </summary>
        public DataFrame ExecuteAsDataFrame()
        {
            var concreteSession = _session as Session;
            if (concreteSession == null)
                throw new InvalidOperationException("Session must be concrete implementation");

            var sourceDf = concreteSession.GetDataFrame(_sourceName);
            
            var resultDf = sourceDf;
            foreach (var operation in _operations)
            {
                resultDf = operation(resultDf);
            }
            
            return resultDf;
        }
    }

    /// <summary>
    /// 比较操作符
    /// </summary>
    public enum ComparisonOp
    {
        Gt,    // 大于
        Ge,    // 大于等于
        Lt,    // 小于
        Le,    // 小于等于
        Eq,    // 等于
        Ne,    // 不等于
        Contains,    // 包含
        StartsWith,  // 以...开头
        EndsWith     // 以...结尾
    }

    /// <summary>
    /// 聚合函数
    /// </summary>
    public enum AggregateFunction
    {
        Sum,
        Average,
        Min,
        Max,
        Count
    }

    /// <summary>
    /// Session DataFrame查询扩展方法
    /// </summary>
    public static class SessionDataFrameQueryExtensions
    {
        /// <summary>
        /// 创建DataFrame查询构建器
        /// </summary>
        public static SessionDataFrameQueryBuilder QueryDataFrame(this ISession session, string name)
        {
            return new SessionDataFrameQueryBuilder(session, name);
        }

        /// <summary>
        /// 快速过滤查询（数值）
        /// </summary>
        public static IDataSet FilterDataFrame(this ISession session, string sourceName, string columnName, ComparisonOp op, double value, string resultName)
        {
            return session.QueryDataFrame(sourceName)
                .Where(columnName, op, value)
                .Execute(resultName);
        }

        /// <summary>
        /// 快速过滤查询（字符串）
        /// </summary>
        public static IDataSet FilterDataFrame(this ISession session, string sourceName, string columnName, ComparisonOp op, string value, string resultName)
        {
            return session.QueryDataFrame(sourceName)
                .Where(columnName, op, value)
                .Execute(resultName);
        }

        /// <summary>
        /// 快速选择查询
        /// </summary>
        public static IDataSet SelectDataFrame(this ISession session, string sourceName, string[] columns, string resultName)
        {
            return session.QueryDataFrame(sourceName)
                .Select(columns)
                .Execute(resultName);
        }
    }
}