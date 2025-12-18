using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;
using NumSharp;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// DataFrame适配器，实现IDataSet接口，在Session内部使用DataFrame作为高性能数据处理引擎
    /// </summary>
    public class DataFrameAdapter : IDataSet
    {
        private readonly DataFrame _dataFrame;
        private readonly string _name;
        private readonly string _id;

        public DataFrameAdapter(string name, DataFrame dataFrame)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _dataFrame = dataFrame ?? throw new ArgumentNullException(nameof(dataFrame));
            _id = Guid.NewGuid().ToString("N");
        }

        public string Name => _name;
        public DataSetKind Kind => DataSetKind.Tabular;
        public string Id => _id;

        /// <summary>
        /// 获取内部的DataFrame对象
        /// </summary>
        public DataFrame DataFrame => _dataFrame;

        /// <summary>
        /// 数据集行数
        /// </summary>
        public long RowCount => _dataFrame.Rows.Count;

        /// <summary>
        /// 数据集列数
        /// </summary>
        public int ColumnCount => _dataFrame.Columns.Count;

        /// <summary>
        /// 列名列表
        /// </summary>
        public IReadOnlyCollection<string> ColumnNames => _dataFrame.Columns.Select(c => c.Name).ToList();

        public IDataSet WithName(string name)
        {
            return new DataFrameAdapter(name, _dataFrame.Clone());
        }

        /// <summary>
        /// 将DataFrame转换为现有的TabularData格式
        /// </summary>
        public Tabular.TabularData ToTabularData()
        {
            var tabular = new Tabular.TabularData(_name);
            
            foreach (var column in _dataFrame.Columns)
            {
                try
                {
                    // 尝试处理数值列
                    if (column is PrimitiveDataFrameColumn<double> doubleColumn)
                    {
                        var values = new double[doubleColumn.Length];
                        for (long i = 0; i < doubleColumn.Length; i++)
                        {
                            values[i] = doubleColumn[i].GetValueOrDefault(0.0);
                        }
                        tabular.AddNumericColumn(column.Name, np.array(values));
                    }
                    else if (column is PrimitiveDataFrameColumn<float> floatColumn)
                    {
                        var values = new double[floatColumn.Length];
                        for (long i = 0; i < floatColumn.Length; i++)
                        {
                            values[i] = (double)floatColumn[i].GetValueOrDefault(0.0f);
                        }
                        tabular.AddNumericColumn(column.Name, np.array(values));
                    }
                    else if (column is PrimitiveDataFrameColumn<int> intColumn)
                    {
                        var values = new double[intColumn.Length];
                        for (long i = 0; i < intColumn.Length; i++)
                        {
                            values[i] = (double)intColumn[i].GetValueOrDefault(0);
                        }
                        tabular.AddNumericColumn(column.Name, np.array(values));
                    }
                    else if (column is StringDataFrameColumn stringColumn)
                    {
                        var values = stringColumn.ToArray();
                        tabular.AddStringColumn(column.Name, values);
                    }
                    else if (column is BooleanDataFrameColumn boolColumn)
                    {
                        var values = new string[boolColumn.Length];
                        for (long i = 0; i < boolColumn.Length; i++)
                        {
                            var value = boolColumn[i];
                            values[i] = value.HasValue ? (value.Value ? "True" : "False") : "";
                        }
                        tabular.AddStringColumn(column.Name, values);
                    }
                    else
                    {
                        // 通用处理：转换为字符串
                        var values = new string[column.Length];
                        for (long i = 0; i < column.Length; i++)
                        {
                            var value = column[i];
                            values[i] = value?.ToString() ?? "";
                        }
                        tabular.AddStringColumn(column.Name, values);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"Failed to convert column {column.Name}: {ex.Message}");
                }
            }
            
            return tabular;
        }

        /// <summary>
        /// 获取数值列数据
        /// </summary>
        public double[] GetNumericColumn(string columnName)
        {
            if (_dataFrame.Columns.IndexOf(columnName) == -1)
                throw new ArgumentException($"Column '{columnName}' not found");

            var column = _dataFrame.Columns[columnName];
            
            if (column is PrimitiveDataFrameColumn<double> doubleColumn)
            {
                var values = new double[doubleColumn.Length];
                for (long i = 0; i < doubleColumn.Length; i++)
                {
                    values[i] = doubleColumn[i].GetValueOrDefault(0.0);
                }
                return values;
            }
            else if (column is PrimitiveDataFrameColumn<float> floatColumn)
            {
                var values = new double[floatColumn.Length];
                for (long i = 0; i < floatColumn.Length; i++)
                {
                    values[i] = (double)floatColumn[i].GetValueOrDefault(0.0f);
                }
                return values;
            }
            else if (column is PrimitiveDataFrameColumn<int> intColumn)
            {
                var values = new double[intColumn.Length];
                for (long i = 0; i < intColumn.Length; i++)
                {
                    values[i] = (double)intColumn[i].GetValueOrDefault(0);
                }
                return values;
            }
            else
            {
                throw new InvalidOperationException($"Column '{columnName}' is not a numeric type");
            }
        }

        /// <summary>
        /// 获取字符串列数据
        /// </summary>
        public string[] GetStringColumn(string columnName)
        {
            if (_dataFrame.Columns.IndexOf(columnName) == -1)
                throw new ArgumentException($"Column '{columnName}' not found");

            var column = _dataFrame.Columns[columnName];
            
            if (column is StringDataFrameColumn stringColumn)
            {
                return stringColumn.ToArray();
            }
            else
            {
                // 其他类型转换为字符串
                var values = new string[column.Length];
                for (long i = 0; i < column.Length; i++)
                {
                    var value = column[i];
                    values[i] = value?.ToString() ?? "";
                }
                return values;
            }
        }

        /// <summary>
        /// 检查是否包含指定列
        /// </summary>
        public bool HasColumn(string columnName)
        {
            return _dataFrame.Columns.IndexOf(columnName) != -1;
        }

        /// <summary>
        /// 获取列的数据类型
        /// </summary>
        public Type GetColumnType(string columnName)
        {
            if (_dataFrame.Columns.IndexOf(columnName) == -1)
                throw new ArgumentException($"Column '{columnName}' not found");

            return _dataFrame.Columns[columnName].DataType;
        }

        /// <summary>
        /// 获取数据统计信息
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var stats = new Dictionary<string, object>
            {
                ["RowCount"] = RowCount,
                ["ColumnCount"] = ColumnCount,
                ["ColumnNames"] = ColumnNames.ToArray()
            };

            // 为数值列添加统计信息
            foreach (var column in _dataFrame.Columns)
            {
                if (column is PrimitiveDataFrameColumn<double> numericColumn)
                {
                    var columnStats = new Dictionary<string, object>
                    {
                        ["Min"] = numericColumn.Min(),
                        ["Max"] = numericColumn.Max(),
                        ["Mean"] = numericColumn.Mean(),
                        ["Sum"] = numericColumn.Sum(),
                        ["StdDev"] = CalculateStandardDeviation(numericColumn)
                    };
                    stats[column.Name] = columnStats;
                }
            }

            return stats;
        }

        private double CalculateStandardDeviation(PrimitiveDataFrameColumn<double> column)
        {
            if (column.Length == 0) return 0;
            
            var mean = column.Mean();
            var sumSquaredDifferences = column.Sum(v => Math.Pow((v ?? 0.0) - mean, 2));
            return Math.Sqrt((double)sumSquaredDifferences / column.Length);
        }

        /// <summary>
        /// 创建DataFrame的深拷贝
        /// </summary>
        public DataFrameAdapter Clone()
        {
            return new DataFrameAdapter(_name, _dataFrame.Clone());
        }

        public override string ToString()
        {
            return $"DataFrameAdapter: {_name} ({RowCount} rows, {ColumnCount} columns)";
        }
    }
}