using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AroAro.DataCore.Session;
using AroAro.DataCore.Workspace;
using Microsoft.Data.Analysis;

namespace AroAro.DataCore.Tools
{
    /// <summary>
    /// Agent-facing dispatch layer — 平的静态方法，参数全基本类型。
    /// 每个方法对应一个 Agent tool，middleware 按 function name 路由到这里。
    /// </summary>
    public static class DataCoreTools
    {
        private static DataCoreStore _store;

        /// <summary>
        /// 初始化工具层，绑定到 DataCoreStore 实例
        /// </summary>
        public static void Initialize(DataCoreStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// 返回所有 tool 的 JSON Schema，供 Agent 框架自动注册。
        /// </summary>
        public static string GetToolSchemas()
        {
            var schemas = new List<object>
            {
                ToolSchema("workspace_create", "创建命名 workspace", new[] { Param("name","string","工作区名称",true) }),
                ToolSchema("workspace_destroy", "销毁命名 workspace", new[] { Param("name","string","工作区名称",true) }),
                ToolSchema("workspace_list", "列出所有 workspace", Array.Empty<object>()),
                ToolSchema("workspace_open", "从 store 加载（复制语义）", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_open_ref", "从 store 加载（零拷贝）", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true) }),
                ToolSchema("workspace_import_csv", "从 CSV 导入", new[] { Param("workspace","string","工作区",false,"default"), Param("csv","string","CSV 内容",true), Param("name","string","数据集名称",true), Param("hasHeader","boolean","含表头",false,true) }),
                ToolSchema("workspace_describe", "数据集描述", new[] { Param("workspace","string","工作区",false,"default"), Param("name","string","数据集名称",false) }),
                ToolSchema("workspace_sample", "查看样例行", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("count","integer","行数",false,10), Param("offset","integer","偏移",false,0) }),
                ToolSchema("workspace_schema", "列 schema", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true) }),
                ToolSchema("workspace_statistics", "统计摘要", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true) }),
                ToolSchema("workspace_filter", "过滤行", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("filter","string","过滤表达式",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_select", "选择列", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("columns","array","列名列表",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_rename_columns", "重命名列", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("columns","object","映射 {old:new}",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_sort", "排序", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("column","string","排序列",true), Param("ascending","boolean","升序",false,true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_distinct", "去重", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("columns","array","去重列",false), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_add_column", "新增计算列", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("column","string","新列名",true), Param("expression","string","表达式",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_drop_columns", "删除列", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("columns","array","要删除的列",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_limit", "限制行数", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("count","integer","行数",true), Param("offset","integer","偏移",false,0), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_random_sample", "随机采样", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("count","integer","采样数",true), Param("seed","integer","种子",false), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_join", "Join", new[] { Param("workspace","string","工作区",false,"default"), Param("left","string","左表",true), Param("right","string","右表",true), Param("leftKey","string","左键",true), Param("rightKey","string","右键",false), Param("how","string","inner/left/right/full",false,"inner"), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_union", "上下拼接", new[] { Param("workspace","string","工作区",false,"default"), Param("left","string","上表",true), Param("right","string","下表",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_cross", "笛卡尔积", new[] { Param("workspace","string","工作区",false,"default"), Param("left","string","左表",true), Param("right","string","右表",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_aggregate", "Group by 聚合", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("groupBy","array","分组列",true), Param("aggregations","array","聚合配置",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_summarize", "全局聚合", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("aggregations","array","聚合配置",true) }),
                ToolSchema("workspace_count", "计数", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("filter","string","过滤条件",false) }),
                ToolSchema("workspace_save", "推回 store", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true) }),
                ToolSchema("workspace_export_csv", "导出 CSV", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true) }),
                ToolSchema("workspace_export_json", "导出 JSON", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("format","string","records/columns/values",false,"records") }),
                ToolSchema("workspace_update", "更新行", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("filter","string","过滤条件",true), Param("set","object","更新值",true) }),
                ToolSchema("workspace_delete_rows", "删除行", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("filter","string","过滤条件",true) }),
                ToolSchema("workspace_append", "追加行", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("rows","array","行数据",true) }),
                ToolSchema("workspace_clear", "清空数据集", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true) }),
                ToolSchema("workspace_clone", "克隆数据集", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("resultName","string","结果名称",true) }),
                ToolSchema("workspace_rename_dataset", "重命名", new[] { Param("workspace","string","工作区",false,"default"), Param("source","string","原名称",true), Param("newName","string","新名称",true) }),
                ToolSchema("workspace_remove", "移除数据集", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true) }),
                ToolSchema("workspace_search", "搜索数据集", new[] { Param("workspace","string","工作区",false,"default"), Param("query","string","关键词",true) }),
                ToolSchema("workspace_diff", "比较数据集", new[] { Param("workspace","string","工作区",false,"default"), Param("left","string","左表",true), Param("right","string","右表",true) }),
                ToolSchema("workspace_dataframe_create", "创建 DataFrame", new[] { Param("workspace","string","工作区",false,"default"), Param("name","string","名称",true) }),
                ToolSchema("workspace_dataframe_convert", "表格转 DataFrame", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true) }),
                ToolSchema("workspace_dataframe_list", "列出 DataFrame", new[] { Param("workspace","string","工作区",false,"default") }),
                ToolSchema("workspace_dataframe_remove", "移除 DataFrame", new[] { Param("workspace","string","工作区",false,"default"), Param("name","string","名称",true) }),
                ToolSchema("workspace_dataframe_to_dataset", "DataFrame 转表格", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","DataFrame 名称",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_open_graph", "加载图到 Workspace", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","图数据集",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_add_nodes", "添加节点", new[] { Param("workspace","string","工作区",false,"default"), Param("graph","string","图名称",true), Param("nodes","array","节点列表",true) }),
                ToolSchema("workspace_add_edges", "添加边", new[] { Param("workspace","string","工作区",false,"default"), Param("graph","string","图名称",true), Param("edges","array","边列表",true) }),
                ToolSchema("workspace_graph_neighbors", "邻居查询", new[] { Param("workspace","string","工作区",false,"default"), Param("graph","string","图名称",true), Param("nodeId","string","节点 ID",true), Param("direction","string","in/out/all",false,"out") }),
                ToolSchema("workspace_describe_graph", "描述图", new[] { Param("workspace","string","工作区",false,"default"), Param("graph","string","图名称",true) }),

                // ── Phase 7: 扩展 ──
                ToolSchema("workspace_value_counts", "某列值计数（频次分布）", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("column","string","列名",true), Param("top","integer","返回前 N 个",false,20), Param("ascending","boolean","升序",false,false) }),
                ToolSchema("workspace_cast_column", "列类型转换 (string↔numeric)", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("column","string","列名",true), Param("type","string","目标类型: numeric/string",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_fill_null", "空值填充", new[] { Param("workspace","string","工作区",false,"default"), Param("dataset","string","数据集名称",true), Param("column","string","列名（省略则全部列）",false), Param("value","string","填充值",true), Param("resultName","string","结果名称",false) }),
                ToolSchema("workspace_graph_path", "图最短路径查找 (BFS)", new[] { Param("workspace","string","工作区",false,"default"), Param("graph","string","图名称",true), Param("from","string","起始节点",true), Param("to","string","目标节点",true), Param("maxDepth","integer","最大深度",false,10) }),
                ToolSchema("workspace_graph_stats", "图统计信息（度分布、连通分量）", new[] { Param("workspace","string","工作区",false,"default"), Param("graph","string","图名称",true) }),
                ToolSchema("workspace_batch", "批量执行多个 tool（减少 Agent 调用次数）", new[] { Param("workspace","string","工作区",false,"default"), Param("steps","array","步骤列表 [{tool, args}]",true) })
            };

            return JsonSerializer.Serialize(schemas, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// 返回所有注册的 tool 名称列表
        /// </summary>
        public static IReadOnlyCollection<string> GetToolNames()
        {
            return new[]
            {
                "workspace_create", "workspace_destroy", "workspace_list",
                "workspace_open", "workspace_open_ref", "workspace_import_csv",
                "workspace_describe", "workspace_sample", "workspace_schema", "workspace_statistics",
                "workspace_filter", "workspace_select", "workspace_rename_columns",
                "workspace_sort", "workspace_distinct", "workspace_add_column",
                "workspace_drop_columns", "workspace_limit", "workspace_random_sample",
                "workspace_join", "workspace_union", "workspace_cross",
                "workspace_aggregate", "workspace_summarize", "workspace_count",
                "workspace_save", "workspace_export_csv", "workspace_export_json",
                "workspace_update", "workspace_delete_rows", "workspace_append", "workspace_clear",
                "workspace_clone", "workspace_rename_dataset", "workspace_remove",
                "workspace_search", "workspace_diff",
                "workspace_dataframe_create", "workspace_dataframe_convert",
                "workspace_dataframe_list", "workspace_dataframe_remove", "workspace_dataframe_to_dataset",
                "workspace_open_graph", "workspace_add_nodes", "workspace_add_edges",
                "workspace_graph_neighbors", "workspace_describe_graph",
                "workspace_value_counts", "workspace_cast_column", "workspace_fill_null",
                "workspace_graph_path", "workspace_graph_stats", "workspace_batch"
            };
        }

        private static object ToolSchema(string name, string description, object[] parameters)
        {
            var props = new Dictionary<string, object>();
            var required = new List<string>();
            foreach (var p in parameters)
            {
                var pd = (Dictionary<string, object>)p;
                var pn = (string)pd["name"];
                var ps = new Dictionary<string, object> { ["type"] = pd["type"], ["description"] = pd["description"] };
                if (pd.ContainsKey("default")) ps["default"] = pd["default"];
                props[pn] = ps;
                if ((bool)pd["required"]) required.Add(pn);
            }
            return new Dictionary<string, object>
            {
                ["name"] = name, ["description"] = description,
                ["parameters"] = new Dictionary<string, object>
                {
                    ["type"] = "object", ["properties"] = props, ["required"] = required
                }
            };
        }

        private static object Param(string name, string type, string description, bool required = false, object defaultValue = null)
        {
            var d = new Dictionary<string, object> { ["name"] = name, ["type"] = type, ["description"] = description, ["required"] = required };
            if (defaultValue != null) d["default"] = defaultValue;
            return d;
        }

        /// <summary>
        /// 路由入口：按 tool name 分发到具体方法
        /// </summary>
        public static string Execute(string toolName, Dictionary<string, object> args)
        {
            try
            {
                return toolName switch
                {
                    // 管理
                    "workspace_create" => WorkspaceCreate(args),
                    "workspace_destroy" => WorkspaceDestroy(args),
                    "workspace_list" => WorkspaceList(args),

                    // 加载
                    "workspace_open" => WorkspaceOpen(args),
                    "workspace_open_ref" => WorkspaceOpenRef(args),
                    "workspace_import_csv" => WorkspaceImportCsv(args),

                    // 检视
                    "workspace_describe" => WorkspaceDescribe(args),
                    "workspace_sample" => WorkspaceSample(args),
                    "workspace_schema" => WorkspaceSchema(args),
                    "workspace_statistics" => WorkspaceStatistics(args),

                    // 变换
                    "workspace_filter" => WorkspaceFilter(args),
                    "workspace_select" => WorkspaceSelect(args),
                    "workspace_rename_columns" => WorkspaceRenameColumns(args),
                    "workspace_sort" => WorkspaceSort(args),
                    "workspace_distinct" => WorkspaceDistinct(args),
                    "workspace_add_column" => WorkspaceAddColumn(args),
                    "workspace_drop_columns" => WorkspaceDropColumns(args),
                    "workspace_limit" => WorkspaceLimit(args),
                    "workspace_random_sample" => WorkspaceRandomSample(args),

                    // 组合
                    "workspace_join" => WorkspaceJoin(args),
                    "workspace_union" => WorkspaceUnion(args),
                    "workspace_cross" => WorkspaceCross(args),

                    // 聚合
                    "workspace_aggregate" => WorkspaceAggregate(args),
                    "workspace_summarize" => WorkspaceSummarize(args),
                    "workspace_count" => WorkspaceCount(args),

                    // 持久化
                    "workspace_save" => WorkspaceSave(args),
                    "workspace_export_csv" => WorkspaceExportCsv(args),
                    "workspace_export_json" => WorkspaceExportJson(args),

                    // 修改
                    "workspace_update" => WorkspaceUpdate(args),
                    "workspace_delete_rows" => WorkspaceDeleteRows(args),
                    "workspace_append" => WorkspaceAppend(args),
                    "workspace_clear" => WorkspaceClear(args),

                    // 元操作
                    "workspace_clone" => WorkspaceClone(args),
                    "workspace_rename_dataset" => WorkspaceRenameDataset(args),
                    "workspace_remove" => WorkspaceRemove(args),
                    "workspace_search" => WorkspaceSearch(args),
                    "workspace_diff" => WorkspaceDiff(args),

                    // DataFrame
                    "workspace_dataframe_create" => WorkspaceDataFrameCreate(args),
                    "workspace_dataframe_convert" => WorkspaceDataFrameConvert(args),
                    "workspace_dataframe_list" => WorkspaceDataFrameList(args),
                    "workspace_dataframe_remove" => WorkspaceDataFrameRemove(args),
                    "workspace_dataframe_to_dataset" => WorkspaceDataFrameToDataset(args),

                    // 图数据
                    "workspace_open_graph" => WorkspaceOpenGraph(args),
                    "workspace_add_nodes" => WorkspaceAddNodes(args),
                    "workspace_add_edges" => WorkspaceAddEdges(args),
                    "workspace_graph_neighbors" => WorkspaceGraphNeighbors(args),
                    "workspace_describe_graph" => WorkspaceDescribeGraph(args),

                    // Phase 7: 扩展
                    "workspace_value_counts" => WorkspaceValueCounts(args),
                    "workspace_cast_column" => WorkspaceCastColumn(args),
                    "workspace_fill_null" => WorkspaceFillNull(args),
                    "workspace_graph_path" => WorkspaceGraphPath(args),
                    "workspace_graph_stats" => WorkspaceGraphStats(args),
                    "workspace_batch" => WorkspaceBatch(args),

                    _ => ToolResult.Fail(toolName, $"Unknown tool: {toolName}",
                        "Use workspace_list to see available tools.").ToJson()
                };
            }
            catch (KeyNotFoundException ex)
            {
                // Dataset not found — try to add available names as suggestion
                string suggestion = null;
                try
                {
                    var ws = GetWorkspace(args);
                    suggestion = Suggest(ws, ex.Message);
                }
                catch { }
                return ToolResult.Fail(toolName, ex.Message, suggestion).ToJson();
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(toolName, ex.Message).ToJson();
            }
        }

        #region Helper

        private static IWorkspace GetWorkspace(Dictionary<string, object> args)
        {
            var wsName = GetString(args, "workspace", "default");
            if (wsName == "default")
                return _store.Workspace;
            return _store.GetWorkspace(wsName);
        }

        /// <summary>
        /// 统一获取数据集名称。优先 "dataset"，fallback "source"（向后兼容）。
        /// </summary>
        private static string GetDatasetName(Dictionary<string, object> args, bool required = true)
        {
            if (args.TryGetValue("dataset", out var d) && d != null)
                return d.ToString();
            if (args.TryGetValue("source", out var s) && s != null)
                return s.ToString();
            if (required)
                throw new ArgumentException("Missing required parameter: 'dataset' (or legacy 'source')");
            return null;
        }

        /// <summary>
        /// 统一获取图数据集名称。优先 "graph"，fallback "dataset"。
        /// </summary>
        private static string GetGraphName(Dictionary<string, object> args)
        {
            if (args.TryGetValue("graph", out var g) && g != null)
                return g.ToString();
            if (args.TryGetValue("dataset", out var d) && d != null)
                return d.ToString();
            throw new ArgumentException("Missing required parameter: 'graph' (or 'dataset')");
        }

        /// <summary>
        /// 生成 "Did you mean X?" suggestion
        /// </summary>
        private static string Suggest(IWorkspace ws, string name, string extra = null)
        {
            var all = ws.AllNames.ToList();
            if (all.Count == 0) return extra;

            // Simple edit-distance match
            string best = null;
            int bestDist = int.MaxValue;
            foreach (var n in all)
            {
                var dist = Levenshtein(name.ToLower(), n.ToLower());
                if (dist < bestDist) { bestDist = dist; best = n; }
            }

            if (best != null && bestDist <= 3 && bestDist > 0)
                return $"Did you mean '{best}'?";

            return $"Available: {string.Join(", ", all.Take(10))}";
        }

        private static int Levenshtein(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }

        /// <summary>
        /// 获取 tabular 数据集，失败时返回带 suggestion 的错误 JSON
        /// </summary>
        private static (ITabularDataset tabular, string errorJson) GetTabularOrError(
            IWorkspace ws, string name, string action)
        {
            var ds = ws.Get(name);
            if (ds is ITabularDataset tabular)
                return (tabular, null);
            return (null, ToolResult.Fail(action,
                $"'{name}' is not a tabular dataset (it's {ds.Kind})",
                $"This tool only works with tabular data. Use workspace_describe_graph for graphs.").ToJson());
        }

        private static string GetString(Dictionary<string, object> args, string key, string defaultValue = null)
        {
            if (args.TryGetValue(key, out var v) && v != null)
                return v.ToString();
            if (defaultValue != null)
                return defaultValue;
            throw new ArgumentException($"Missing required parameter: {key}");
        }

        private static string GetOptionalString(Dictionary<string, object> args, string key, string defaultValue = null)
        {
            if (args.TryGetValue(key, out var v) && v != null)
                return v.ToString();
            return defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue = 0)
        {
            if (args.TryGetValue(key, out var v) && v != null)
                return Convert.ToInt32(v);
            return defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue = false)
        {
            if (args.TryGetValue(key, out var v) && v != null)
                return Convert.ToBoolean(v);
            return defaultValue;
        }

        private static string[] GetStringArray(Dictionary<string, object> args, string key)
        {
            if (!args.TryGetValue(key, out var v) || v == null)
                return Array.Empty<string>();

            if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
                return je.EnumerateArray().Select(e => e.GetString()).ToArray();

            if (v is IEnumerable<object> enumerable)
                return enumerable.Select(x => x.ToString()).ToArray();

            return new[] { v.ToString() };
        }

        private static Dictionary<string, string> GetStringMap(Dictionary<string, object> args, string key)
        {
            if (!args.TryGetValue(key, out var v) || v == null)
                return new Dictionary<string, string>();

            if (v is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                var map = new Dictionary<string, string>();
                foreach (var prop in je.EnumerateObject())
                    map[prop.Name] = prop.Value.GetString();
                return map;
            }

            if (v is Dictionary<string, object> dict)
                return dict.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");

            return new Dictionary<string, string>();
        }

        private static List<Dictionary<string, object>> GetRowArray(Dictionary<string, object> args, string key)
        {
            if (!args.TryGetValue(key, out var v) || v == null)
                return new List<Dictionary<string, object>>();

            if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<Dictionary<string, object>>();
                foreach (var item in je.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var row = new Dictionary<string, object>();
                        foreach (var prop in item.EnumerateObject())
                            row[prop.Name] = JsonElementToObject(prop.Value);
                        rows.Add(row);
                    }
                }
                return rows;
            }

            if (v is IEnumerable<Dictionary<string, object>> dictEnum)
                return dictEnum.ToList();

            if (v is IEnumerable<object> objEnum)
            {
                var rows = new List<Dictionary<string, object>>();
                foreach (var item in objEnum)
                {
                    if (item is Dictionary<string, object> d) rows.Add(d);
                    else if (item is IDictionary<string, object> id) rows.Add(id.ToDictionary(kv => kv.Key, kv => kv.Value));
                }
                return rows;
            }

            return new List<Dictionary<string, object>>();
        }

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }

        private static List<Dictionary<string, object>> GetAggregations(Dictionary<string, object> args, string key)
        {
            return GetRowArray(args, key);
        }

        private static int _tempCounter;

        private static string TempName(string baseName) => $"__tmp_{baseName}_{++_tempCounter}";

        /// <summary>
        /// 从 ITabularDataset 导出样例行
        /// </summary>
        private static List<Dictionary<string, object>> GetSample(ITabularDataset tabular, int count, int offset = 0)
        {
            var sample = new List<Dictionary<string, object>>();
            var end = Math.Min(offset + count, tabular.RowCount);
            for (int i = offset; i < end; i++)
                sample.Add(tabular.GetRow(i));
            return sample;
        }

        /// <summary>
        /// 创建数据集的副本（复制语义）。用临时名称避免与 store 已有数据集冲突。
        /// </summary>
        private static ITabularDataset CopyTabular(ITabularDataset source, DataCoreStore store)
        {
            var tmpName = TempName(source.Name);
            var copy = store.CreateTabular(tmpName);

            // Direct data copy — avoids CSV serialization/deserialization overhead
            var rows = source.Query().ToDictionaries();
            if (rows.Any())
            {
                var first = rows.First();
                foreach (var col in source.ColumnNames)
                {
                    var colType = source.GetColumnType(col);
                    if (colType == ColumnType.Numeric)
                        copy.AddNumericColumn(col, new double[0]);
                    else
                        copy.AddStringColumn(col, new string[0]);
                }
                copy.AddRows(rows);
            }

            return copy;
        }

        /// <summary>
        /// 将结果注册到 workspace 并返回描述
        /// </summary>
        private static string RegisterAndDescribe(IWorkspace ws, string name, IDataSet dataset,
            DataSource source = DataSource.Derived, string derivedFrom = null, string operation = null)
        {
            ws.Register(name, dataset, source);

            var tabular = dataset as ITabularDataset;
            var result = new Dictionary<string, object>
            {
                ["name"] = name,
                ["rows"] = tabular?.RowCount ?? 0,
                ["columns"] = tabular?.ColumnCount ?? 0,
                ["columnNames"] = tabular?.ColumnNames?.ToList() ?? new List<string>(),
                ["source"] = source.ToString()
            };

            if (derivedFrom != null) result["derivedFrom"] = derivedFrom;
            if (operation != null) result["operation"] = operation;

            if (tabular != null && tabular.RowCount > 0)
                result["sample"] = GetSample(tabular, 3);

            return ToolResult.Ok("_", result).ToJson();
        }

        #endregion

        #region 3.1 管理

        public static string WorkspaceCreate(Dictionary<string, object> args)
        {
            var name = GetString(args, "name");
            _store.CreateWorkspace(name);
            return ToolResult.Ok("workspace_create", new { name, created = true }).ToJson();
        }

        public static string WorkspaceDestroy(Dictionary<string, object> args)
        {
            var name = GetString(args, "name");
            _store.DestroyWorkspace(name);
            return ToolResult.Ok("workspace_destroy", new { name, destroyed = true }).ToJson();
        }

        public static string WorkspaceList(Dictionary<string, object> args)
        {
            var workspaces = _store.ListWorkspaces()
                .Select(entry => new
                {
                    name = entry.Name,
                    datasetCount = entry.Workspace.DatasetCount,
                    summary = entry.Workspace.Summary()
                })
                .ToList();
            return ToolResult.Ok("workspace_list", new { workspaces }).ToJson();
        }

        #endregion

        #region 3.2 加载

        public static string WorkspaceOpen(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");

            if (!_store.TryGet(datasetName, out var storeDs))
                return ToolResult.Fail("workspace_open",
                    $"Dataset '{datasetName}' not found in store",
                    $"Available datasets: {string.Join(", ", _store.Names)}").ToJson();

            // 复制语义
            IDataSet copy;
            if (storeDs is ITabularDataset tabular)
            {
                copy = CopyTabular(tabular, _store);
            }
            else if (storeDs is IGraphDataset graph)
            {
                var newGraph = _store.CreateGraph(datasetName + "_ws_copy");
                foreach (var nodeId in graph.GetNodeIds())
                {
                    var props = graph.GetNodeProperties(nodeId);
                    newGraph.AddNode(nodeId, props as IDictionary<string, object>);
                }
                foreach (var edge in graph.GetEdges())
                {
                    var props = graph.GetEdgeProperties(edge.From, edge.To);
                    newGraph.AddEdge(edge.From, edge.To, props);
                }
                copy = newGraph;
            }
            else
            {
                return ToolResult.Fail("workspace_open", $"Unsupported dataset kind: {storeDs.Kind}").ToJson();
            }

            ws.Register(datasetName, copy, DataSource.Store);
            return DescribeSingleDataset(ws, datasetName, "workspace_open");
        }

        public static string WorkspaceOpenRef(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");

            if (!_store.TryGet(datasetName, out var storeDs))
                return ToolResult.Fail("workspace_open_ref",
                    $"Dataset '{datasetName}' not found in store",
                    $"Available datasets: {string.Join(", ", _store.Names)}").ToJson();

            // 零拷贝引用
            ws.Register(datasetName, storeDs, DataSource.Store);
            return DescribeSingleDataset(ws, datasetName, "workspace_open_ref");
        }

        public static string WorkspaceImportCsv(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var csv = GetString(args, "csv");
            var name = GetString(args, "name");
            var hasHeader = GetBool(args, "hasHeader", true);
            var delimiter = GetString(args, "delimiter", ",");

            var tabular = _store.CreateTabular(name);
            tabular.ImportFromCsv(csv, hasHeader, delimiter[0]);

            ws.Register(name, tabular, DataSource.Imported);
            return DescribeSingleDataset(ws, name, "workspace_import_csv");
        }

        #endregion

        #region 3.3 检视

        public static string WorkspaceDescribe(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);

            if (args.TryGetValue("dataset", out var dsObj) && dsObj != null && !string.IsNullOrWhiteSpace(dsObj.ToString()))
            {
                var dsName = dsObj.ToString();
                return DescribeSingleDataset(ws, dsName, "workspace_describe");
            }

            // Describe all
            var all = ws.DescribeAll();
            var datasets = all.Select(e => new
            {
                name = e.Name,
                kind = e.Kind.ToString(),
                rows = e.Rows,
                columns = e.Columns,
                source = e.Source.ToString()
            }).ToList();

            return ToolResult.Ok("workspace_describe", new
            {
                summary = ws.Summary(),
                datasets
            }).ToJson();
        }

        public static string WorkspaceSample(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var dsName = GetString(args, "dataset");
            var rows = GetInt(args, "rows", 5);
            var offset = GetInt(args, "offset", 0);

            var ds = ws.Get(dsName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_sample", $"\'{dsName}\' is not a tabular dataset (kind: {ws.Get(dsName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var sample = GetSample(tabular, rows, offset);
            return ToolResult.Ok("workspace_sample", new
            {
                dataset = dsName,
                offset,
                rowsRequested = rows,
                rowsReturned = sample.Count,
                totalRows = tabular.RowCount,
                data = sample
            }).ToJson();
        }

        public static string WorkspaceSchema(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var dsName = GetString(args, "dataset");

            var ds = ws.Get(dsName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_schema", $"\'{dsName}\' is not a tabular dataset (kind: {ws.Get(dsName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var columns = tabular.ColumnNames.Select(colName =>
            {
                var colType = tabular.GetColumnType(colName);
                var schema = new Dictionary<string, object>
                {
                    ["name"] = colName,
                    ["type"] = colType.ToString()
                };

                // Add sample values
                try
                {
                    var sampleCount = Math.Min(3, tabular.RowCount);
                    var samples = new List<object>();
                    for (int i = 0; i < sampleCount; i++)
                    {
                        var row = tabular.GetRow(i);
                        if (row.TryGetValue(colName, out var val))
                            samples.Add(val);
                    }
                    schema["sample"] = samples;
                }
                catch { }

                return schema;
            }).ToList();

            return ToolResult.Ok("workspace_schema", new
            {
                dataset = dsName,
                columns,
                totalColumns = tabular.ColumnCount,
                totalRows = tabular.RowCount
            }).ToJson();
        }

        public static string WorkspaceStatistics(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var dsName = GetString(args, "dataset");
            var columns = GetStringArray(args, "columns");

            var ds = ws.Get(dsName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_statistics", $"\'{dsName}\' is not a tabular dataset (kind: {ws.Get(dsName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            if (columns.Length == 0)
                columns = tabular.ColumnNames.ToArray();

            var stats = new Dictionary<string, object>();
            foreach (var col in columns)
            {
                if (!tabular.HasColumn(col)) continue;

                var colType = tabular.GetColumnType(col);
                var colStats = new Dictionary<string, object>();

                try
                {
                    colStats["count"] = tabular.RowCount;
                    // Count nulls by checking empty strings / zero
                    int nulls = 0;
                    if (colType == ColumnType.Numeric)
                    {
                        var data = tabular.GetNumericColumnRaw(col);
                        nulls = data.Count(v => v == 0);
                        colStats["min"] = data.Min();
                        colStats["max"] = data.Max();
                        colStats["mean"] = data.Average();
                        if (data.Length > 1)
                            colStats["std"] = tabular.Std(col);
                        colStats["distinct"] = data.Distinct().Count();
                    }
                    else if (colType == ColumnType.String)
                    {
                        var data = tabular.GetStringColumn(col);
                        nulls = data.Count(string.IsNullOrEmpty);
                        colStats["distinct"] = data.Where(s => !string.IsNullOrEmpty(s)).Distinct().Count();
                        colStats["top"] = data.Where(s => !string.IsNullOrEmpty(s))
                            .GroupBy(s => s)
                            .OrderByDescending(g => g.Count())
                            .Take(5)
                            .Select(g => g.Key)
                            .ToList();
                    }
                    colStats["nulls"] = nulls;
                }
                catch { }

                stats[col] = colStats;
            }

            return ToolResult.Ok("workspace_statistics", new
            {
                dataset = dsName,
                rows = tabular.RowCount,
                columns = stats
            }).ToJson();
        }

        #endregion

        #region 3.4 变换

        public static string WorkspaceFilter(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var filter = GetString(args, "filter");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_filter", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            // Parse and apply filter
            var predicate = FilterExpressionParser.Parse(filter);
            var rows = tabular.Query().ToDictionaries().Where(predicate).ToList();

            // Create result dataset
            var result = _store.CreateTabular(resultName);
            if (rows.Count > 0)
            {
                foreach (var colName in tabular.ColumnNames)
                {
                    var colType = tabular.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, rows.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, rows.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }
            else
            {
                // Empty result — copy schema
                foreach (var colName in tabular.ColumnNames)
                {
                    var colType = tabular.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, Array.Empty<double>());
                    else
                        result.AddStringColumn(colName, Array.Empty<string>());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_filter", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                columnNames = result.ColumnNames.ToList(),
                source,
                filter,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceSelect(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var columns = GetStringArray(args, "columns");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_select", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var selectedRows = tabular.Query().Select(columns).ToDictionaries();
            var result = _store.CreateTabular(resultName);
            if (selectedRows.Count > 0)
            {
                foreach (var colName in columns)
                {
                    if (!tabular.HasColumn(colName)) continue;
                    var colType = tabular.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, selectedRows.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, selectedRows.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_select", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                columnNames = result.ColumnNames.ToList(),
                source,
                selectedColumns = columns,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceRenameColumns(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var mapping = GetStringMap(args, "mapping");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_rename_columns", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            // Export to CSV, rename headers, re-import
            var csv = tabular.ExportToCsv();
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var headers = lines[0].Split(',');
                for (int i = 0; i < headers.Length; i++)
                {
                    var h = headers[i].Trim().Trim('"');
                    if (mapping.TryGetValue(h, out var newName))
                        headers[i] = newName;
                }
                lines[0] = string.Join(",", headers);
            }

            var result = _store.CreateTabular(resultName);
            result.ImportFromCsv(string.Join("\n", lines));
            ws.Register(resultName, result, DataSource.Derived);

            return ToolResult.Ok("workspace_rename_columns", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                columnNames = result.ColumnNames.ToList(),
                source,
                mapping,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceSort(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var by = GetString(args, "by");
            var order = GetString(args, "order", "asc");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_sort", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var query = tabular.Query();
            query = order == "desc" ? query.OrderByDescending(by) : query.OrderBy(by);
            var sorted = query.ToDictionaries();

            var result = _store.CreateTabular(resultName);
            if (sorted.Count > 0)
            {
                foreach (var colName in tabular.ColumnNames)
                {
                    var colType = tabular.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, sorted.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, sorted.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_sort", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                columnNames = result.ColumnNames.ToList(),
                source,
                sortBy = by,
                order,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceDistinct(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var columns = GetStringArray(args, "columns");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_distinct", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var allRows = tabular.Query().ToDictionaries();
            var distinctCols = columns.Length > 0 ? columns : tabular.ColumnNames.ToArray();

            var distinct = allRows
                .GroupBy(r => string.Join("|", distinctCols.Select(c => r.TryGetValue(c, out var v) ? v?.ToString() ?? "" : "")))
                .Select(g => g.First())
                .ToList();

            var result = _store.CreateTabular(resultName);
            if (distinct.Count > 0)
            {
                foreach (var colName in tabular.ColumnNames)
                {
                    var colType = tabular.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, distinct.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, distinct.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_distinct", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                columnNames = result.ColumnNames.ToList(),
                source,
                distinctOn = distinctCols,
                removedDuplicates = allRows.Count - distinct.Count,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceAddColumn(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var columnName = GetString(args, "columnName");
            var expression = GetString(args, "expression");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_add_column", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            // Simple expression evaluation: supports column references and basic arithmetic
            var allRows = tabular.Query().ToDictionaries();
            var result = _store.CreateTabular(resultName);

            // Copy existing columns
            foreach (var colName in tabular.ColumnNames)
            {
                var colType = tabular.GetColumnType(colName);
                if (colType == ColumnType.Numeric)
                    result.AddNumericColumn(colName, allRows.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                else
                    result.AddStringColumn(colName, allRows.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
            }

            // Evaluate expression for each row
            var newValues = new double[allRows.Count];
            var newStringValues = new string[allRows.Count];
            bool isNumeric = true;

            for (int i = 0; i < allRows.Count; i++)
            {
                var val = SimpleExpressionEval.Evaluate(expression, allRows[i]);
                if (val is double d) { newValues[i] = d; }
                else if (val is int n) { newValues[i] = n; }
                else if (val is long l) { newValues[i] = l; }
                else if (val is IConvertible && double.TryParse(val?.ToString() ?? "", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                { newValues[i] = parsed; }
                else { newStringValues[i] = val?.ToString() ?? ""; isNumeric = false; }
            }

            if (isNumeric)
                result.AddNumericColumn(columnName, newValues);
            else
                result.AddStringColumn(columnName, newStringValues);

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_add_column", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                columnNames = result.ColumnNames.ToList(),
                source,
                newColumn = columnName,
                expression,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceDropColumns(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var columns = GetStringArray(args, "columns");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_drop_columns", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var remaining = tabular.ColumnNames.Where(c => !columns.Contains(c)).ToList();
            var allRows = tabular.Query().Select(remaining.ToArray()).ToDictionaries();

            var result = _store.CreateTabular(resultName);
            if (allRows.Count > 0)
            {
                foreach (var colName in remaining)
                {
                    var colType = tabular.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, allRows.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, allRows.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_drop_columns", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                columnNames = result.ColumnNames.ToList(),
                source,
                droppedColumns = columns,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceLimit(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var count = GetInt(args, "count");
            var offset = GetInt(args, "offset", 0);
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_limit", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var limited = tabular.Query().Skip(offset).Limit(count).ToDictionaries();

            var result = _store.CreateTabular(resultName);
            if (limited.Count > 0)
            {
                foreach (var colName in tabular.ColumnNames)
                {
                    var colType = tabular.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, limited.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, limited.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_limit", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                source,
                offset,
                limit = count,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceRandomSample(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var count = GetInt(args, "count");
            var seed = GetInt(args, "seed", -1);
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_random_sample", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var allRows = tabular.Query().ToDictionaries();
            var rng = seed >= 0 ? new Random(seed) : new Random();
            var sampled = allRows.OrderBy(_ => rng.Next()).Take(count).ToList();

            var result = _store.CreateTabular(resultName);
            if (sampled.Count > 0)
            {
                foreach (var colName in tabular.ColumnNames)
                {
                    var colType = tabular.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, sampled.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, sampled.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_random_sample", new
            {
                name = resultName,
                rows = result.RowCount,
                source,
                sampleCount = count,
                seed,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        #endregion

        #region 3.5 组合

        public static string WorkspaceJoin(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var leftName = GetString(args, "left");
            var rightName = GetString(args, "right");
            var key = args.TryGetValue("key", out var kObj) ? kObj?.ToString() : null;
            var leftKey = args.TryGetValue("leftKey", out var lkObj) ? lkObj?.ToString() : key;
            var rightKey = args.TryGetValue("rightKey", out var rkObj) ? rkObj?.ToString() : key;
            if (leftKey == null || rightKey == null)
                throw new ArgumentException("Either 'key' or both 'leftKey'/'rightKey' are required");
            var joinType = GetString(args, "joinType", "inner");
            var resultName = GetString(args, "resultName");

            var leftDs = ws.Get(leftName);
            var rightDs = ws.Get(rightName);

            if (leftDs is not ITabularDataset leftTab)
                return ToolResult.Fail("workspace_join", $"\'{leftName}\' is not a tabular dataset (kind: {ws.Get(leftName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();
            if (rightDs is not ITabularDataset rightTab)
                return ToolResult.Fail("workspace_join", $"\'{rightName}\' is not a tabular dataset (kind: {ws.Get(rightName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var leftRows = leftTab.Query().ToDictionaries();
            var rightRows = rightTab.Query().ToDictionaries();

            // Build right index by rightKey
            var rightIndex = new Dictionary<string, List<Dictionary<string, object>>>();
            foreach (var r in rightRows)
            {
                var k = r.TryGetValue(rightKey, out var v) ? v?.ToString() ?? "" : "";
                if (!rightIndex.ContainsKey(k)) rightIndex[k] = new List<Dictionary<string, object>>();
                rightIndex[k].Add(r);
            }

            // Determine result columns: left columns + right columns (excluding rightKey from right)
            var resultColumns = leftTab.ColumnNames.ToList();
            var rightExtraColumns = rightTab.ColumnNames.Where(c => c != rightKey).ToList();
            resultColumns.AddRange(rightExtraColumns);

            var joined = new List<Dictionary<string, object>>();
            var matchedRightKeys = new HashSet<string>();
            int unmatchedLeft = 0;

            foreach (var lRow in leftRows)
            {
                var k = lRow.TryGetValue(leftKey, out var v) ? v?.ToString() ?? "" : "";
                if (rightIndex.TryGetValue(k, out var matches))
                {
                    matchedRightKeys.Add(k);
                    foreach (var rRow in matches)
                    {
                        var merged = new Dictionary<string, object>(lRow);
                        foreach (var col in rightExtraColumns)
                            merged[col] = rRow.TryGetValue(col, out var rv) ? rv : null;
                        joined.Add(merged);
                    }
                }
                else
                {
                    unmatchedLeft++;
                    if (joinType == "left" || joinType == "full")
                    {
                        var merged = new Dictionary<string, object>(lRow);
                        foreach (var col in rightExtraColumns)
                            merged[col] = null;
                        joined.Add(merged);
                    }
                }
            }

            // Full/right join: add unmatched right rows
            if (joinType == "right" || joinType == "full")
            {
                foreach (var rRow in rightRows)
                {
                    var k = rRow.TryGetValue(rightKey, out var v) ? v?.ToString() ?? "" : "";
                    if (!matchedRightKeys.Contains(k))
                    {
                        var merged = new Dictionary<string, object>();
                        foreach (var col in leftTab.ColumnNames)
                            merged[col] = null;
                        merged[leftKey] = rRow[rightKey];
                        foreach (var col in rightExtraColumns)
                            merged[col] = rRow.TryGetValue(col, out var rv) ? rv : null;
                        joined.Add(merged);
                    }
                }
            }

            int unmatchedRight = rightRows.Count - matchedRightKeys.Count;

            // Create result dataset
            var result = _store.CreateTabular(resultName);
            if (joined.Count > 0)
            {
                foreach (var colName in resultColumns)
                {
                    bool isLeft = leftTab.HasColumn(colName);
                    bool isRight = rightTab.HasColumn(colName);
                    var colType = isLeft ? leftTab.GetColumnType(colName) : (isRight ? rightTab.GetColumnType(colName) : ColumnType.String);

                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, joined.Select(r => r.TryGetValue(colName, out var v) && v != null ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, joined.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_join", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                leftRows = leftRows.Count,
                rightRows = rightRows.Count,
                matchedRows = joined.Count - (joinType == "left" || joinType == "full" ? unmatchedLeft : 0),
                unmatchedLeft,
                unmatchedRight,
                key = leftKey == rightKey ? leftKey : $"{leftKey}={rightKey}",
                joinType,
                columnNames = resultColumns,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceUnion(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var sources = GetStringArray(args, "sources");
            var resultName = GetString(args, "resultName");

            var allRows = new List<Dictionary<string, object>>();
            ITabularDataset firstTab = null;

            foreach (var src in sources)
            {
                var ds = ws.Get(src);
                if (ds is not ITabularDataset tab)
                    return ToolResult.Fail("workspace_union", $"\'{src}\' is not a tabular dataset (kind: {ws.Get(src).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();
                if (firstTab == null) firstTab = tab;
                allRows.AddRange(tab.Query().ToDictionaries());
            }

            var result = _store.CreateTabular(resultName);
            if (allRows.Count > 0 && firstTab != null)
            {
                foreach (var colName in firstTab.ColumnNames)
                {
                    var colType = firstTab.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, allRows.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, allRows.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_union", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                sources,
                sourceRowCounts = sources.Select(s => ws.Get(s) is ITabularDataset t ? t.RowCount : 0).ToList(),
                sample = GetSample(result, 3)
            }).ToJson();
        }

        public static string WorkspaceCross(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var leftName = GetString(args, "left");
            var rightName = GetString(args, "right");
            var resultName = GetString(args, "resultName");

            var leftDs = ws.Get(leftName);
            var rightDs = ws.Get(rightName);

            if (leftDs is not ITabularDataset leftTab)
                return ToolResult.Fail("workspace_cross", $"\'{leftName}\' is not a tabular dataset (kind: {ws.Get(leftName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();
            if (rightDs is not ITabularDataset rightTab)
                return ToolResult.Fail("workspace_cross", $"\'{rightName}\' is not a tabular dataset (kind: {ws.Get(rightName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var leftRows = leftTab.Query().ToDictionaries();
            var rightRows = rightTab.Query().ToDictionaries();

            var cross = new List<Dictionary<string, object>>();
            foreach (var l in leftRows)
            {
                foreach (var r in rightRows)
                {
                    var merged = new Dictionary<string, object>(l);
                    foreach (var kv in r)
                        merged[kv.Key] = kv.Value;
                    cross.Add(merged);
                }
            }

            var resultColumns = leftTab.ColumnNames.Concat(rightTab.ColumnNames).ToList();
            var result = _store.CreateTabular(resultName);
            if (cross.Count > 0)
            {
                foreach (var colName in resultColumns)
                {
                    bool isLeft = leftTab.HasColumn(colName);
                    var colType = isLeft ? leftTab.GetColumnType(colName) : rightTab.GetColumnType(colName);
                    if (colType == ColumnType.Numeric)
                        result.AddNumericColumn(colName, cross.Select(r => r.TryGetValue(colName, out var v) ? Convert.ToDouble(v) : 0.0).ToArray());
                    else
                        result.AddStringColumn(colName, cross.Select(r => r.TryGetValue(colName, out var v) ? v?.ToString() ?? "" : "").ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_cross", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                leftRows = leftRows.Count,
                rightRows = rightRows.Count,
                columnNames = resultColumns,
                sample = GetSample(result, 3)
            }).ToJson();
        }

        #endregion

        #region 3.6 聚合

        public static string WorkspaceAggregate(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var groupBy = GetString(args, "groupBy");
            var aggregations = GetAggregations(args, "aggregations");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_aggregate", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var allRows = tabular.Query().ToDictionaries();
            var groups = allRows.GroupBy(r => r.TryGetValue(groupBy, out var v) ? v?.ToString() ?? "" : "");

            var resultRows = new List<Dictionary<string, object>>();
            foreach (var group in groups)
            {
                var row = new Dictionary<string, object> { [groupBy] = group.Key };
                var groupRows = group.ToList();

                foreach (var agg in aggregations)
                {
                    var col = agg["column"]?.ToString();
                    var op = agg["op"]?.ToString();
                    var alias = agg.ContainsKey("alias") ? agg["alias"]?.ToString() : $"{op}_{col}";

                    var values = groupRows
                        .Where(r => r.TryGetValue(col, out var v) && v != null)
                        .Select(r => Convert.ToDouble(r[col]))
                        .ToList();

                    row[alias] = op switch
                    {
                        "count" => (double)groupRows.Count,
                        "sum" => values.Sum(),
                        "mean" => values.Count > 0 ? values.Average() : 0,
                        "min" => values.Count > 0 ? values.Min() : 0,
                        "max" => values.Count > 0 ? values.Max() : 0,
                        "std" => values.Count > 1 ? Math.Sqrt(values.Sum(v => Math.Pow(v - values.Average(), 2)) / (values.Count - 1)) : 0,
                        _ => 0
                    };
                }
                resultRows.Add(row);
            }

            // Build result dataset
            var result = _store.CreateTabular(resultName);
            if (resultRows.Count > 0)
            {
                // GroupBy column (string)
                result.AddStringColumn(groupBy, resultRows.Select(r => r[groupBy]?.ToString() ?? "").ToArray());

                foreach (var agg in aggregations)
                {
                    var col = agg["column"]?.ToString();
                    var op = agg["op"]?.ToString();
                    var alias = agg.ContainsKey("alias") ? agg["alias"]?.ToString() : $"{op}_{col}";
                    result.AddNumericColumn(alias, resultRows.Select(r => Convert.ToDouble(r[alias])).ToArray());
                }
            }

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_aggregate", new
            {
                name = resultName,
                rows = result.RowCount,
                columns = result.ColumnCount,
                source,
                groupBy,
                aggregations = aggregations.Select(a => a.ContainsKey("alias") ? a["alias"] : $"{a["op"]}_{a["column"]}").ToList(),
                columnNames = result.ColumnNames.ToList(),
                sample = GetSample(result, 5)
            }).ToJson();
        }

        public static string WorkspaceSummarize(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var aggregations = GetAggregations(args, "aggregations");
            var resultName = GetString(args, "resultName");

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_summarize", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var allRows = tabular.Query().ToDictionaries();
            var resultRow = new Dictionary<string, object>();

            foreach (var agg in aggregations)
            {
                var col = agg["column"]?.ToString();
                var op = agg["op"]?.ToString();
                var alias = agg.ContainsKey("alias") ? agg["alias"]?.ToString() : $"{op}_{col}";

                var values = allRows
                    .Where(r => r.TryGetValue(col, out var v) && v != null)
                    .Select(r => Convert.ToDouble(r[col]))
                    .ToList();

                resultRow[alias] = op switch
                {
                    "count" => (double)allRows.Count,
                    "sum" => values.Sum(),
                    "mean" => values.Count > 0 ? values.Average() : 0,
                    "min" => values.Count > 0 ? values.Min() : 0,
                    "max" => values.Count > 0 ? values.Max() : 0,
                    "std" => values.Count > 1 ? Math.Sqrt(values.Sum(v => Math.Pow(v - values.Average(), 2)) / (values.Count - 1)) : 0,
                    _ => 0
                };
            }

            var result = _store.CreateTabular(resultName);
            foreach (var kv in resultRow)
                result.AddNumericColumn(kv.Key, new[] { Convert.ToDouble(kv.Value) });

            ws.Register(resultName, result, DataSource.Derived);
            return ToolResult.Ok("workspace_summarize", new
            {
                name = resultName,
                rows = 1,
                columns = resultRow.Count,
                source,
                aggregations = resultRow
            }).ToJson();
        }

        public static string WorkspaceCount(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var filter = GetString(args, "filter", null);

            var ds = ws.Get(source);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_count", $"\'{source}\' is not a tabular dataset (kind: {ws.Get(source).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            int count;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                var predicate = FilterExpressionParser.Parse(filter);
                count = tabular.Query().ToDictionaries().Count(r => predicate(r));
            }
            else
            {
                count = tabular.RowCount;
            }

            return ToolResult.Ok("workspace_count", new
            {
                dataset = source,
                filter,
                count
            }).ToJson();
        }

        #endregion

        #region 3.7 持久化

        public static string WorkspaceSave(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");
            var newName = GetOptionalString(args, "newName");

            var ds = ws.Get(datasetName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_save", $"\'{datasetName}\' is not a tabular dataset (kind: {ws.Get(datasetName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var saveName = newName ?? datasetName;

            // Export CSV from workspace dataset BEFORE any store operations
            var csv = tabular.ExportToCsv();

            // Delete existing if present and different from source
            if (_store.HasDataset(saveName))
                _store.Delete(saveName);

            var storeCopy = _store.CreateTabular(saveName);
            storeCopy.ImportFromCsv(csv);

            return ToolResult.Ok("workspace_save", new
            {
                name = datasetName,
                savedTo = saveName,
                rows = storeCopy.RowCount,
                columns = storeCopy.ColumnCount,
                sourceChanged = "Derived → Store"
            }).ToJson();
        }

        public static string WorkspaceExportCsv(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");
            var delimiter = GetString(args, "delimiter", ",");
            var includeHeader = GetBool(args, "includeHeader", true);

            var ds = ws.Get(datasetName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_export_csv", $"\'{datasetName}\' is not a tabular dataset (kind: {ws.Get(datasetName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var csv = tabular.ExportToCsv(delimiter[0], includeHeader);
            return ToolResult.Ok("workspace_export_csv", new
            {
                dataset = datasetName,
                rows = tabular.RowCount,
                columns = tabular.ColumnCount,
                content = csv
            }).ToJson();
        }

        public static string WorkspaceExportJson(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");
            var format = GetString(args, "format", "records");

            var ds = ws.Get(datasetName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_export_json", $"\'{datasetName}\' is not a tabular dataset (kind: {ws.Get(datasetName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var rows = tabular.Query().ToDictionaries();
            string json = format switch
            {
                "records" => JsonSerializer.Serialize(rows),
                "columns" => JsonSerializer.Serialize(
                    tabular.ColumnNames.ToDictionary(c => c, c => (object)rows.Select(r => r.TryGetValue(c, out var v) ? v : null).ToList())),
                "values" => JsonSerializer.Serialize(rows.Select(r => r.Values.ToList()).ToList()),
                _ => JsonSerializer.Serialize(rows)
            };

            return ToolResult.Ok("workspace_export_json", new
            {
                dataset = datasetName,
                rows = tabular.RowCount,
                format,
                content = json
            }).ToJson();
        }

        #endregion

        #region 3.8 修改

        public static string WorkspaceUpdate(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");
            var filter = GetString(args, "filter");
            var setValues = GetStringMap(args, "set");

            var ds = ws.Get(datasetName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_update", $"\'{datasetName}\' is not a tabular dataset (kind: {ws.Get(datasetName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var predicate = FilterExpressionParser.Parse(filter);
            int updated = 0;

            for (int i = 0; i < tabular.RowCount; i++)
            {
                var row = tabular.GetRow(i);
                if (predicate(row))
                {
                    var updates = new Dictionary<string, object>();
                    foreach (var kv in setValues)
                        updates[kv.Key] = kv.Value;
                    tabular.UpdateRow(i, updates);
                    updated++;
                }
            }

            return ToolResult.Ok("workspace_update", new
            {
                dataset = datasetName,
                filter,
                updatedRows = updated,
                columns = setValues.Keys.ToList(),
                newValues = setValues
            }).ToJson();
        }

        public static string WorkspaceDeleteRows(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");
            var filter = GetString(args, "filter");

            var ds = ws.Get(datasetName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_delete_rows", $"\'{datasetName}\' is not a tabular dataset (kind: {ws.Get(datasetName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var predicate = FilterExpressionParser.Parse(filter);
            int deleted = 0;

            // Delete from back to front to preserve indices
            for (int i = tabular.RowCount - 1; i >= 0; i--)
            {
                var row = tabular.GetRow(i);
                if (predicate(row))
                {
                    tabular.DeleteRow(i);
                    deleted++;
                }
            }

            return ToolResult.Ok("workspace_delete_rows", new
            {
                dataset = datasetName,
                filter,
                deletedRows = deleted,
                remainingRows = tabular.RowCount
            }).ToJson();
        }

        public static string WorkspaceAppend(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");
            var rawRows = GetRowArray(args, "rows");

            var ds = ws.Get(datasetName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_append", $"\'{datasetName}\' is not a tabular dataset (kind: {ws.Get(datasetName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            // Convert JsonElement values to proper types
            var rows = rawRows.Select(r => r.ToDictionary(kv => kv.Key, kv => ConvertJsonValue(kv.Value))).ToList();

            int added = tabular.AddRows(rows);
            return ToolResult.Ok("workspace_append", new
            {
                dataset = datasetName,
                addedRows = added,
                totalRows = tabular.RowCount
            }).ToJson();
        }

        private static object ConvertJsonValue(object value)
        {
            if (value is JsonElement je)
            {
                return je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString(),
                    JsonValueKind.Number => je.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => je.ToString()
                };
            }
            return value;
        }

        public static string WorkspaceClear(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");

            var ds = ws.Get(datasetName);
            if (ds is not ITabularDataset tabular)
                return ToolResult.Fail("workspace_clear", $"\'{datasetName}\' is not a tabular dataset (kind: {ws.Get(datasetName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            int cleared = tabular.Clear();
            return ToolResult.Ok("workspace_clear", new
            {
                dataset = datasetName,
                clearedRows = cleared,
                columns = tabular.ColumnNames.ToList()
            }).ToJson();
        }

        #endregion

        #region 3.10 元操作

        public static string WorkspaceClone(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var newName = GetOptionalString(args, "newName");

            var cloned = ws.Clone(source, newName);
            return DescribeSingleDataset(ws, newName, "workspace_clone");
        }

        public static string WorkspaceRenameDataset(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var oldName = GetString(args, "oldName");
            var newName = GetOptionalString(args, "newName");

            ws.Rename(oldName, newName);
            return ToolResult.Ok("workspace_rename_dataset", new
            {
                oldName,
                newName,
                renamed = true
            }).ToJson();
        }

        public static string WorkspaceRemove(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var datasetName = GetString(args, "dataset");

            ws.Remove(datasetName);
            return ToolResult.Ok("workspace_remove", new
            {
                dataset = datasetName,
                removed = true
            }).ToJson();
        }

        public static string WorkspaceSearch(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var query = GetString(args, "query");

            var all = ws.DescribeAll();
            var matches = all
                .Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(e => new
                {
                    name = e.Name,
                    source = e.Source.ToString(),
                    rows = e.Rows
                })
                .ToList();

            return ToolResult.Ok("workspace_search", new
            {
                query,
                matches
            }).ToJson();
        }

        public static string WorkspaceDiff(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var leftName = GetString(args, "left");
            var rightName = GetString(args, "right");

            var leftDs = ws.Get(leftName);
            var rightDs = ws.Get(rightName);

            if (leftDs is not ITabularDataset leftTab)
                return ToolResult.Fail("workspace_diff", $"\'{leftName}\' is not a tabular dataset (kind: {ws.Get(leftName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();
            if (rightDs is not ITabularDataset rightTab)
                return ToolResult.Fail("workspace_diff", $"\'{rightName}\' is not a tabular dataset (kind: {ws.Get(rightName).Kind})", "This tool requires tabular data. Use graph tools for graph datasets.").ToJson();

            var leftCols = new HashSet<string>(leftTab.ColumnNames);
            var rightCols = new HashSet<string>(rightTab.ColumnNames);
            var onlyInLeft = leftCols.Where(c => !rightCols.Contains(c)).ToList();
            var onlyInRight = rightCols.Where(c => !leftCols.Contains(c)).ToList();
            var common = leftCols.Where(c => rightCols.Contains(c)).ToList();

            return ToolResult.Ok("workspace_diff", new
            {
                left = new { name = leftName, rows = leftTab.RowCount, columns = leftTab.ColumnCount },
                right = new { name = rightName, rows = rightTab.RowCount, columns = rightTab.ColumnCount },
                rowDiff = leftTab.RowCount > rightTab.RowCount ? $"+{leftTab.RowCount - rightTab.RowCount}" :
                          leftTab.RowCount < rightTab.RowCount ? $"-{rightTab.RowCount - leftTab.RowCount}" : "equal",
                columnDiff = new { onlyInLeft, onlyInRight, common },
                summary = $"{rightName} has {rightTab.RowCount - leftTab.RowCount} more rows. " +
                          (onlyInLeft.Count + onlyInRight.Count > 0
                              ? $"Schema differs: {(onlyInLeft.Count > 0 ? $"left has {string.Join(", ", onlyInLeft)}" : "")} " +
                                $"{(onlyInRight.Count > 0 ? $"right has {string.Join(", ", onlyInRight)}" : "")}"
                              : "Schema identical.")
            }).ToJson();
        }

        #endregion

        #region Internal Helpers

        private static string DescribeSingleDataset(IWorkspace ws, string dsName, string action)
        {
            var entry = ws.Describe(dsName);
            var result = new Dictionary<string, object>
            {
                ["name"] = entry.Name,
                ["kind"] = entry.Kind.ToString(),
                ["rows"] = entry.Rows,
                ["columns"] = entry.Columns,
                ["source"] = entry.Source.ToString()
            };

            if (entry.Schema != null && entry.Schema.Count > 0)
                result["columnNames"] = entry.Schema.Select(s => s.Name).ToList();

            if (entry.Sample != null && entry.Sample.Count > 0)
                result["sample"] = entry.Sample;

            return ToolResult.Ok(action, result).ToJson();
        }

        #endregion

        // ────────────────────────────────────────────────────────────
        // DataFrame Tools (Phase 2)
        // ────────────────────────────────────────────────────────────

        #region DataFrame Tools

        private static string WorkspaceDataFrameCreate(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var name = GetString(args, "name");
            ws.CreateDataFrame(name);
            return ToolResult.Ok("workspace_dataframe_create", new
            {
                name,
                message = $"DataFrame '{name}' created"
            }).ToJson();
        }

        private static string WorkspaceDataFrameConvert(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var df = ws.ConvertToDataFrame(source);
            return ToolResult.Ok("workspace_dataframe_convert", new
            {
                source,
                rows = (long)df.Rows.Count,
                columns = df.Columns.Count,
                columnNames = df.Columns.Select(c => c.Name).ToArray()
            }).ToJson();
        }

        private static string WorkspaceDataFrameList(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var names = ws.DataFrameNames.ToArray();
            return ToolResult.Ok("workspace_dataframe_list", new
            {
                count = ws.DataFrameCount,
                dataFrames = names
            }).ToJson();
        }

        private static string WorkspaceDataFrameRemove(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var name = GetString(args, "name");
            var removed = ws.RemoveDataFrame(name);
            if (!removed)
                return ToolResult.Fail("workspace_dataframe_remove",
                    $"DataFrame '{name}' not found",
                    $"Available: {string.Join(", ", ws.DataFrameNames)}").ToJson();
            return ToolResult.Ok("workspace_dataframe_remove", new { name, removed }).ToJson();
        }

        private static string WorkspaceDataFrameToDataset(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var source = GetDatasetName(args);
            var resultName = GetOptionalString(args, "resultName", source);

            var df = ws.GetDataFrame(source);

            // Convert DataFrame → ITabularDataset via DataFrameAdapter
            var adapter = new DataFrameAdapter(resultName, df);
            var tabularData = adapter.ToTabularData();

            // Register in workspace
            ws.Register(resultName, tabularData, DataSource.Derived);

            return ToolResult.Ok("workspace_dataframe_to_dataset", new
            {
                source,
                resultName,
                rows = (long)df.Rows.Count,
                columns = df.Columns.Count
            }).ToJson();
        }

        #endregion

        // ────────────────────────────────────────────────────────────
        // Graph Tools (Phase 4)
        // ────────────────────────────────────────────────────────────

        #region Graph Tools

        private static string WorkspaceOpenGraph(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var dataset = GetString(args, "dataset");
            var resultName = GetOptionalString(args, "resultName", dataset);

            if (!_store.TryGet(dataset, out var ds) || ds is not IGraphDataset graph)
                return ToolResult.Fail("workspace_open_graph",
                    $"Graph dataset '{dataset}' not found in store",
                    $"Available graphs: {string.Join(", ", _store.GraphNames.Take(10))}").ToJson();

            // Clone graph data into workspace
            var newGraph = _store.CreateGraph("__ws_" + resultName);
            foreach (var nodeId in graph.GetNodeIds())
            {
                var props = graph.GetNodeProperties(nodeId);
                newGraph.AddNode(nodeId, props as IDictionary<string, object>);
            }
            foreach (var edge in graph.GetEdges())
            {
                var props = graph.GetEdgeProperties(edge.From, edge.To);
                newGraph.AddEdge(edge.From, edge.To, props);
            }

            ws.Register(resultName, newGraph, DataSource.Store);
            return ToolResult.Ok("workspace_open_graph", new
            {
                name = resultName,
                nodes = newGraph.NodeCount,
                edges = newGraph.EdgeCount,
                source = "Store"
            }).ToJson();
        }

        private static string WorkspaceAddNodes(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var graphName = GetGraphName(args);
            var nodes = GetRowArray(args, "nodes");

            var ds = ws.Get(graphName);
            if (ds is not IGraphDataset graph)
                return ToolResult.Fail("workspace_add_nodes",
                    $"'{graphName}' is not a graph dataset").ToJson();

            var toAdd = nodes.Select(n =>
            {
                var id = n.TryGetValue("id", out var v) ? v.ToString() : Guid.NewGuid().ToString("N");
                var props = n.ContainsKey("id") ? n.Where(kv => kv.Key != "id")
                    .ToDictionary(kv => kv.Key, kv => kv.Value) as IDictionary<string, object> : null;
                return (id, props);
            }).ToList();

            var added = graph.AddNodes(toAdd);
            return ToolResult.Ok("workspace_add_nodes", new
            {
                graph = graphName,
                added,
                totalNodes = graph.NodeCount
            }).ToJson();
        }

        private static string WorkspaceAddEdges(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var graphName = GetGraphName(args);
            var edges = GetRowArray(args, "edges");

            var ds = ws.Get(graphName);
            if (ds is not IGraphDataset graph)
                return ToolResult.Fail("workspace_add_edges",
                    $"'{graphName}' is not a graph dataset").ToJson();

            var toAdd = new List<(string, string, IDictionary<string, object>)>();
            foreach (var e in edges)
            {
                if (!e.TryGetValue("from", out var from) || !e.TryGetValue("to", out var to))
                    continue;
                var props = e.Where(kv => kv.Key != "from" && kv.Key != "to")
                    .ToDictionary(kv => kv.Key, kv => kv.Value) as IDictionary<string, object>;
                toAdd.Add((from.ToString(), to.ToString(), props));
            }

            var added = graph.AddEdges(toAdd);
            return ToolResult.Ok("workspace_add_edges", new
            {
                graph = graphName,
                added,
                totalEdges = graph.EdgeCount
            }).ToJson();
        }

        private static string WorkspaceGraphNeighbors(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var graphName = GetGraphName(args);
            var nodeId = GetString(args, "nodeId");
            var direction = GetOptionalString(args, "direction", "out");

            var ds = ws.Get(graphName);
            if (ds is not IGraphDataset graph)
                return ToolResult.Fail("workspace_graph_neighbors",
                    $"'{graphName}' is not a graph dataset").ToJson();

            if (!graph.HasNode(nodeId))
                return ToolResult.Fail("workspace_graph_neighbors",
                    $"Node '{nodeId}' not found in graph '{graphName}'").ToJson();

            IEnumerable<string> neighbors = direction?.ToLower() switch
            {
                "in" => graph.GetInNeighbors(nodeId),
                "all" => graph.GetNeighbors(nodeId),
                _ => graph.GetOutNeighbors(nodeId)
            };

            var neighborList = neighbors.ToList();
            var neighborDetails = neighborList.Select(n => new
            {
                id = n,
                properties = graph.GetNodeProperties(n)
            }).ToList();

            return ToolResult.Ok("workspace_graph_neighbors", new
            {
                graph = graphName,
                nodeId,
                direction,
                count = neighborList.Count,
                neighbors = neighborDetails
            }).ToJson();
        }

        private static string WorkspaceDescribeGraph(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var graphName = GetGraphName(args);

            var ds = ws.Get(graphName);
            if (ds is not IGraphDataset graph)
                return ToolResult.Fail("workspace_describe_graph",
                    $"'{graphName}' is not a graph dataset").ToJson();

            // Sample nodes
            var nodeSample = graph.GetNodeIds().Take(5).Select(id => new
            {
                id,
                properties = graph.GetNodeProperties(id)
            }).ToList();

            // Sample edges
            var edgeSample = graph.GetEdges().Take(5).Select(e => new
            {
                from = e.From,
                to = e.To,
                properties = graph.GetEdgeProperties(e.From, e.To)
            }).ToList();

            return ToolResult.Ok("workspace_describe_graph", new
            {
                name = graphName,
                nodes = graph.NodeCount,
                edges = graph.EdgeCount,
                nodeSample,
                edgeSample
            }).ToJson();
        }

        #endregion

        // ────────────────────────────────────────────────────────────
        // Phase 7: 扩展 Tools
        // ────────────────────────────────────────────────────────────

        #region Phase 7 Tools

        private static string WorkspaceValueCounts(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var name = GetDatasetName(args);
            var column = GetString(args, "column");
            var top = GetInt(args, "top", 20);
            var ascending = GetBool(args, "ascending", false);

            var (tabular, err) = GetTabularOrError(ws, name, "workspace_value_counts");
            if (err != null) return err;

            if (!tabular.HasColumn(column))
                return ToolResult.Fail("workspace_value_counts",
                    $"Column '{column}' not found in '{name}'",
                    $"Available columns: {string.Join(", ", tabular.ColumnNames)}").ToJson();

            // Count values
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < tabular.RowCount; i++)
            {
                var row = tabular.GetRow(i);
                var val = row.TryGetValue(column, out var v) ? v?.ToString() ?? "(null)" : "(null)";
                counts[val] = counts.GetValueOrDefault(val, 0) + 1;
            }

            var sorted = ascending
                ? counts.OrderBy(kv => kv.Value).Take(top)
                : counts.OrderByDescending(kv => kv.Value).Take(top);

            var result = sorted.Select(kv => new { value = kv.Key, count = kv.Value }).ToList();

            return ToolResult.Ok("workspace_value_counts", new
            {
                dataset = name,
                column,
                totalValues = tabular.RowCount,
                uniqueValues = counts.Count,
                top = result.Count,
                ascending,
                data = result
            }).ToJson();
        }

        private static string WorkspaceCastColumn(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var name = GetDatasetName(args);
            var column = GetString(args, "column");
            var targetType = GetString(args, "type").ToLower();
            var resultName = GetOptionalString(args, "resultName", name);

            var (tabular, err) = GetTabularOrError(ws, name, "workspace_cast_column");
            if (err != null) return err;

            if (!tabular.HasColumn(column))
                return ToolResult.Fail("workspace_cast_column",
                    $"Column '{column}' not found",
                    $"Available: {string.Join(", ", tabular.ColumnNames)}").ToJson();

            if (targetType != "numeric" && targetType != "string")
                return ToolResult.Fail("workspace_cast_column",
                    $"Invalid type '{targetType}'. Must be 'numeric' or 'string'").ToJson();

            // Copy all data
            var rows = tabular.Query().ToDictionaries();
            var newTabular = _store.CreateTabular("__cast_" + resultName);

            foreach (var col in tabular.ColumnNames)
            {
                if (col == column) continue;
                var ct = tabular.GetColumnType(col);
                if (ct == ColumnType.Numeric)
                    newTabular.AddNumericColumn(col, new double[0]);
                else
                    newTabular.AddStringColumn(col, new string[0]);
            }

            // Add converted column
            if (targetType == "numeric")
            {
                newTabular.AddNumericColumn(column, new double[0]);
                var convertedRows = rows.Select(r =>
                {
                    var newRow = new Dictionary<string, object>(r);
                    if (newRow.TryGetValue(column, out var v) && v != null)
                    {
                        if (double.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var d))
                            newRow[column] = d;
                        else
                            newRow[column] = 0.0;
                    }
                    else newRow[column] = 0.0;
                    return (IDictionary<string, object>)newRow;
                });
                newTabular.AddRows(convertedRows);
            }
            else
            {
                newTabular.AddStringColumn(column, new string[0]);
                var convertedRows = rows.Select(r =>
                {
                    var newRow = new Dictionary<string, object>(r);
                    if (newRow.TryGetValue(column, out var v))
                        newRow[column] = v?.ToString() ?? "";
                    else
                        newRow[column] = "";
                    return (IDictionary<string, object>)newRow;
                });
                newTabular.AddRows(convertedRows);
            }

            ws.Register(resultName, newTabular, DataSource.Derived);
            return ToolResult.Ok("workspace_cast_column", new
            {
                dataset = resultName,
                column,
                fromType = tabular.GetColumnType(column).ToString(),
                toType = targetType,
                rows = newTabular.RowCount
            }).ToJson();
        }

        private static string WorkspaceFillNull(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var name = GetDatasetName(args);
            var column = GetOptionalString(args, "column");
            var fillValue = GetString(args, "value");
            var resultName = GetOptionalString(args, "resultName", name);

            var (tabular, err) = GetTabularOrError(ws, name, "workspace_fill_null");
            if (err != null) return err;

            var rows = tabular.Query().ToDictionaries();
            var columns = column != null ? new[] { column } : tabular.ColumnNames.ToArray();

            foreach (var col in columns)
            {
                if (!tabular.HasColumn(col))
                    return ToolResult.Fail("workspace_fill_null",
                        $"Column '{col}' not found",
                        $"Available: {string.Join(", ", tabular.ColumnNames)}").ToJson();
            }

            int filled = 0;
            foreach (var row in rows)
            {
                foreach (var col in columns)
                {
                    if (!row.ContainsKey(col) || row[col] == null)
                    {
                        var colType = tabular.GetColumnType(col);
                        row[col] = colType == ColumnType.Numeric
                            ? (object)(double.TryParse(fillValue, out var d) ? d : 0.0)
                            : fillValue;
                        filled++;
                    }
                }
            }

            var newTabular = _store.CreateTabular("__fill_" + resultName);
            foreach (var col in tabular.ColumnNames)
            {
                var ct = tabular.GetColumnType(col);
                if (ct == ColumnType.Numeric)
                    newTabular.AddNumericColumn(col, rows.Select(r => r.TryGetValue(col, out var v) && v != null ? Convert.ToDouble(v) : 0.0).ToArray());
                else
                    newTabular.AddStringColumn(col, rows.Select(r => r.TryGetValue(col, out var v) ? v?.ToString() ?? "" : "").ToArray());
            }

            ws.Register(resultName, newTabular, DataSource.Derived);
            return ToolResult.Ok("workspace_fill_null", new
            {
                dataset = resultName,
                columns = columns.Length == 1 ? columns[0] : "all",
                fillValue,
                cellsFilled = filled,
                rows = newTabular.RowCount
            }).ToJson();
        }

        private static string WorkspaceGraphPath(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var graphName = GetGraphName(args);
            var from = GetString(args, "from");
            var to = GetString(args, "to");
            var maxDepth = GetInt(args, "maxDepth", 10);

            var ds = ws.Get(graphName);
            if (ds is not IGraphDataset graph)
                return ToolResult.Fail("workspace_graph_path",
                    $"'{graphName}' is not a graph dataset").ToJson();

            if (!graph.HasNode(from))
                return ToolResult.Fail("workspace_graph_path",
                    $"Node '{from}' not found", $"Available nodes: {string.Join(", ", graph.GetNodeIds().Take(10))}").ToJson();
            if (!graph.HasNode(to))
                return ToolResult.Fail("workspace_graph_path",
                    $"Node '{to}' not found", $"Available nodes: {string.Join(", ", graph.GetNodeIds().Take(10))}").ToJson();

            // BFS shortest path
            var visited = new HashSet<string> { from };
            var queue = new Queue<List<string>>();
            queue.Enqueue(new List<string> { from });
            List<string> foundPath = null;

            while (queue.Count > 0 && foundPath == null)
            {
                var path = queue.Dequeue();
                if (path.Count > maxDepth) continue;

                var current = path[^1];
                foreach (var neighbor in graph.GetOutNeighbors(current))
                {
                    if (neighbor == to)
                    {
                        foundPath = new List<string>(path) { neighbor };
                        break;
                    }
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        var newPath = new List<string>(path) { neighbor };
                        queue.Enqueue(newPath);
                    }
                }
            }

            if (foundPath == null)
                return ToolResult.Ok("workspace_graph_path", new
                {
                    graph = graphName,
                    from,
                    to,
                    found = false,
                    message = $"No path found from '{from}' to '{to}' within depth {maxDepth}"
                }).ToJson();

            // Get edge properties along path
            var edges = new List<object>();
            for (int i = 0; i < foundPath.Count - 1; i++)
            {
                edges.Add(new
                {
                    from = foundPath[i],
                    to = foundPath[i + 1],
                    properties = graph.GetEdgeProperties(foundPath[i], foundPath[i + 1])
                });
            }

            return ToolResult.Ok("workspace_graph_path", new
            {
                graph = graphName,
                from,
                to,
                found = true,
                length = foundPath.Count - 1,
                path = foundPath,
                edges
            }).ToJson();
        }

        private static string WorkspaceGraphStats(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var graphName = GetGraphName(args);

            var ds = ws.Get(graphName);
            if (ds is not IGraphDataset graph)
                return ToolResult.Fail("workspace_graph_stats",
                    $"'{graphName}' is not a graph dataset").ToJson();

            // Degree stats
            var nodeIds = graph.GetNodeIds().ToList();
            var outDegrees = new List<int>();
            var inDegrees = new List<int>();
            foreach (var id in nodeIds)
            {
                outDegrees.Add(graph.GetOutDegree(id));
                inDegrees.Add(graph.GetInDegree(id));
            }

            // Connected components (BFS)
            var visited = new HashSet<string>();
            int components = 0;
            foreach (var id in nodeIds)
            {
                if (visited.Contains(id)) continue;
                components++;
                var queue = new Queue<string>();
                queue.Enqueue(id);
                while (queue.Count > 0)
                {
                    var curr = queue.Dequeue();
                    if (!visited.Add(curr)) continue;
                    foreach (var n in graph.GetNeighbors(curr))
                        if (!visited.Contains(n)) queue.Enqueue(n);
                }
            }

            // Top nodes by out-degree
            var topOut = nodeIds
                .Select(id => new { id, outDeg = graph.GetOutDegree(id) })
                .OrderByDescending(x => x.outDeg)
                .Take(5)
                .ToList();

            return ToolResult.Ok("workspace_graph_stats", new
            {
                graph = graphName,
                nodes = graph.NodeCount,
                edges = graph.EdgeCount,
                connectedComponents = components,
                avgOutDegree = outDegrees.Count > 0 ? outDegrees.Average() : 0,
                maxOutDegree = outDegrees.Count > 0 ? outDegrees.Max() : 0,
                avgInDegree = inDegrees.Count > 0 ? inDegrees.Average() : 0,
                maxInDegree = inDegrees.Count > 0 ? inDegrees.Max() : 0,
                topByOutDegree = topOut,
                density = nodeIds.Count > 1
                    ? (double)graph.EdgeCount / (nodeIds.Count * (nodeIds.Count - 1))
                    : 0
            }).ToJson();
        }

        private static string WorkspaceBatch(Dictionary<string, object> args)
        {
            var ws = GetWorkspace(args);
            var steps = GetRowArray(args, "steps");

            if (steps.Count == 0)
                return ToolResult.Fail("workspace_batch", "No steps provided").ToJson();
            if (steps.Count > 20)
                return ToolResult.Fail("workspace_batch", "Maximum 20 steps per batch").ToJson();

            var results = new List<object>();
            int succeeded = 0;
            int failed = 0;

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (!step.TryGetValue("tool", out var toolObj))
                {
                    results.Add(new { step = i, success = false, error = "Missing 'tool' field" });
                    failed++;
                    continue;
                }

                var toolName = toolObj.ToString();
                var stepArgs = new Dictionary<string, object>();

                // Inherit workspace from batch
                stepArgs["workspace"] = ws == _store.Workspace ? "default" :
                    _store.ListWorkspaces().FirstOrDefault(w => w.Workspace == ws).Name ?? "default";

                // Merge step args
                if (step.TryGetValue("args", out var argsObj) && argsObj is JsonElement argsJe && argsJe.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in argsJe.EnumerateObject())
                        stepArgs[prop.Name] = prop.Value;
                }
                else if (step.TryGetValue("args", out var argsDict) && argsDict is Dictionary<string, object> d)
                {
                    foreach (var kv in d)
                        stepArgs[kv.Key] = kv.Value;
                }

                try
                {
                    var result = Execute(toolName, stepArgs);
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(result);
                    var success = parsed.TryGetProperty("success", out var s) && s.GetBoolean();
                    if (success) succeeded++; else failed++;

                    object stepResult;
                    if (parsed.TryGetProperty("result", out var r))
                        stepResult = r;
                    else if (parsed.TryGetProperty("error", out var e))
                        stepResult = new { error = e.GetString() };
                    else
                        stepResult = null;

                    results.Add(new { step = i, tool = toolName, success, result = stepResult });
                }
                catch (Exception ex)
                {
                    results.Add(new { step = i, tool = toolName, success = false, error = ex.Message });
                    failed++;
                }
            }

            return ToolResult.Ok("workspace_batch", new
            {
                totalSteps = steps.Count,
                succeeded,
                failed,
                results
            }).ToJson();
        }

        #endregion
    }

    /// <summary>
    /// 简单表达式求值器 — 支持列引用和基本算术
    /// 用于 workspace_add_column 的 expression 参数
    ///
    /// 支持:
    ///   列引用: age, score
    ///   算术: age * 1.1, score + 10, price - discount
    ///   三元: age >= 18 ? adult : minor
    ///   常量: 42, hello
    /// </summary>
    internal static class SimpleExpressionEval
    {
        public static object Evaluate(string expression, Dictionary<string, object> row)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return null;

            expression = expression.Trim();

            // Ternary: condition ? trueVal : falseVal
            var qMark = FindTernaryOperator(expression);
            if (qMark >= 0)
            {
                var colon = FindColon(expression, qMark + 1);
                if (colon >= 0)
                {
                    var cond = expression[..qMark].Trim();
                    var trueVal = expression[(qMark + 1)..colon].Trim();
                    var falseVal = expression[(colon + 1)..].Trim();

                    var condResult = EvaluateCondition(cond, row);
                    return condResult
                        ? EvaluateOperand(trueVal, row)
                        : EvaluateOperand(falseVal, row);
                }
            }

            // Simple arithmetic: operand op operand
            var (left, op, right) = FindBinaryOp(expression);
            if (op != null)
            {
                var l = Convert.ToDouble(EvaluateOperand(left.Trim(), row));
                var r = Convert.ToDouble(EvaluateOperand(right.Trim(), row));
                return op switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" => r != 0 ? l / r : 0,
                    "%" => r != 0 ? l % r : 0,
                    _ => (object)0
                };
            }

            // Single operand
            return EvaluateOperand(expression, row);
        }

        private static bool EvaluateCondition(string cond, Dictionary<string, object> row)
        {
            // Try: operand op operand
            var (left, op, right) = FindComparisonOp(cond);
            if (op != null)
            {
                var l = EvaluateOperand(left.Trim(), row);
                var r = EvaluateOperand(right.Trim(), row);

                if (double.TryParse(l?.ToString(), out var ln) && double.TryParse(r?.ToString(), out var rn))
                {
                    return op switch
                    {
                        ">" => ln > rn,
                        ">=" => ln >= rn,
                        "<" => ln < rn,
                        "<=" => ln <= rn,
                        "==" => ln == rn,
                        "!=" => ln != rn,
                        _ => false
                    };
                }

                var ls = l?.ToString() ?? "";
                var rs = r?.ToString() ?? "";
                return op switch
                {
                    "==" => ls == rs,
                    "!=" => ls != rs,
                    _ => false
                };
            }

            return Convert.ToBoolean(EvaluateOperand(cond, row));
        }

        private static object EvaluateOperand(string token, Dictionary<string, object> row)
        {
            token = token.Trim();

            // String literal
            if ((token.StartsWith("'") && token.EndsWith("'")) ||
                (token.StartsWith("\"") && token.EndsWith("\"")))
                return token[1..^1];

            // Numeric literal
            if (double.TryParse(token, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
                return num;

            // Boolean literal
            if (token == "true") return true;
            if (token == "false") return false;

            // Column reference
            if (row.TryGetValue(token, out var val))
                return val;

            return token;
        }

        private static int FindTernaryOperator(string expr)
        {
            int depth = 0;
            for (int i = 0; i < expr.Length; i++)
            {
                if (expr[i] == '(') depth++;
                else if (expr[i] == ')') depth--;
                else if (expr[i] == '?' && depth == 0) return i;
            }
            return -1;
        }

        private static int FindColon(string expr, int start)
        {
            int depth = 0;
            for (int i = start; i < expr.Length; i++)
            {
                if (expr[i] == '(') depth++;
                else if (expr[i] == ')') depth--;
                else if (expr[i] == ':' && depth == 0) return i;
            }
            return -1;
        }

        private static (string left, string op, string right) FindBinaryOp(string expr)
        {
            int depth = 0;
            // Scan right-to-left for + - (lowest precedence)
            for (int i = expr.Length - 1; i >= 0; i--)
            {
                if (expr[i] == ')') depth++;
                else if (expr[i] == '(') depth--;
                else if (depth == 0 && (expr[i] == '+' || expr[i] == '-') && i > 0 && i < expr.Length - 1)
                {
                    // Not a unary minus at the start
                    if (expr[i] == '-' && i == 0) continue;
                    return (expr[..i], expr[i].ToString(), expr[(i + 1)..]);
                }
            }
            // * / %
            depth = 0;
            for (int i = expr.Length - 1; i >= 0; i--)
            {
                if (expr[i] == ')') depth++;
                else if (expr[i] == '(') depth--;
                else if (depth == 0 && (expr[i] == '*' || expr[i] == '/' || expr[i] == '%') && i > 0)
                    return (expr[..i], expr[i].ToString(), expr[(i + 1)..]);
            }
            return (expr, null, null);
        }

        private static (string left, string op, string right) FindComparisonOp(string expr)
        {
            int depth = 0;
            for (int i = 0; i < expr.Length; i++)
            {
                if (expr[i] == '(') depth++;
                else if (expr[i] == ')') depth--;
                else if (depth == 0)
                {
                    if (expr[i] == '>' && i + 1 < expr.Length && expr[i + 1] == '=')
                        return (expr[..i], ">=", expr[(i + 2)..]);
                    if (expr[i] == '<' && i + 1 < expr.Length && expr[i + 1] == '=')
                        return (expr[..i], "<=", expr[(i + 2)..]);
                    if (expr[i] == '!' && i + 1 < expr.Length && expr[i + 1] == '=')
                        return (expr[..i], "!=", expr[(i + 2)..]);
                    if (expr[i] == '=' && i + 1 < expr.Length && expr[i + 1] == '=')
                        return (expr[..i], "==", expr[(i + 2)..]);
                    if (expr[i] == '>')
                        return (expr[..i], ">", expr[(i + 1)..]);
                    if (expr[i] == '<')
                        return (expr[..i], "<", expr[(i + 1)..]);
                }
            }
            return (expr, null, null);
        }
    }
}
