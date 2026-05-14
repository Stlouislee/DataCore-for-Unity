namespace AroAro.DataCore.Workspace
{
    /// <summary>
    /// 列信息，用于 Workspace 自省
    /// </summary>
    public class ColumnInfo
    {
        public string Name { get; set; }
        public ColumnType Type { get; set; }

        public ColumnInfo() { }

        public ColumnInfo(string name, ColumnType type)
        {
            Name = name;
            Type = type;
        }
    }
}
