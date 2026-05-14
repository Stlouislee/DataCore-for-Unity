using System.Collections.Generic;

namespace AroAro.DataCore.Workspace
{
    /// <summary>
    /// 数据集自省结果，用于 Describe / DescribeAll
    /// </summary>
    public class WorkspaceEntry
    {
        /// <summary>数据集名称</summary>
        public string Name { get; set; }

        /// <summary>数据集类型（Tabular / Graph）</summary>
        public DataSetKind Kind { get; set; }

        /// <summary>数据来源</summary>
        public DataSource Source { get; set; }

        /// <summary>行数</summary>
        public int Rows { get; set; }

        /// <summary>列数</summary>
        public int Columns { get; set; }

        /// <summary>列信息（仅 Tabular）</summary>
        public IReadOnlyList<ColumnInfo> Schema { get; set; }

        /// <summary>前 3 行样例数据（仅 Tabular）</summary>
        public IReadOnlyList<Dictionary<string, object>> Sample { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Kind}, {Source}, {Rows}×{Columns})";
        }
    }
}
