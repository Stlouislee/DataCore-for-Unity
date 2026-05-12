using UnityEngine;
using System.Collections.Generic;
using AroAro.DataCore.Workspace;

namespace AroAro.DataCore.Examples
{
    /// <summary>
    /// Workspace 使用示例 — 替代 Session 的默认工作区
    /// </summary>
    public class WorkspaceExample
    {
        public static void RunExample()
        {
            // 创建数据核心存储 — Workspace 自动可用
            using var store = new DataCoreStore();
            var ws = store.Workspace;

            // ── 1. 从字典数据注册 ──
            var salesData = new List<Dictionary<string, object>>
            {
                new() { ["product"] = "Widget", ["region"] = "North", ["amount"] = 150.0 },
                new() { ["product"] = "Gadget", ["region"] = "South", ["amount"] = 230.0 },
                new() { ["product"] = "Widget", ["region"] = "South", ["amount"] = 180.0 },
                new() { ["product"] = "Gadget", ["region"] = "North", ["amount"] = 310.0 },
            };
            ws.Register("sales", salesData, DataSource.Imported);
            Debug.Log($"Registered 'sales': {ws.Describe("sales")}");

            // ── 2. 从 store 加载（自动 fallback） ──
            var storeData = store.CreateTabular("inventory");
            storeData.AddStringColumn("product", new[] { "Widget", "Gadget" });
            storeData.AddNumericColumn("stock", new[] { 100.0, 50.0 });

            var inventory = ws.Get("inventory"); // 自动从 store 加载
            Debug.Log($"Loaded 'inventory' from store: {inventory.Kind}");

            // ── 3. 注册计算结果 ──
            var filtered = new List<Dictionary<string, object>>
            {
                new() { ["product"] = "Gadget", ["region"] = "South", ["amount"] = 230.0 },
                new() { ["product"] = "Gadget", ["region"] = "North", ["amount"] = 310.0 },
            };
            ws.Register("high-value", filtered, DataSource.Derived);
            ws.RegisterAuto("high-value", store.CreateTabular("__tmp")); // 自动命名 → "high-value_2"

            // ── 4. 自省：AI Agent 一句话看全部 ──
            var all = ws.DescribeAll();
            Debug.Log($"Workspace has {all.Count} datasets:");
            foreach (var entry in all)
            {
                Debug.Log($"  {entry.Name} ({entry.Source}, {entry.Rows}×{entry.Columns})");
            }

            // ── 5. 一句话摘要 ──
            Debug.Log(ws.Summary());
            // → "Workspace: 4 datasets (1 store, 2 derived, 1 imported)"

            // ── 6. 生命周期管理 ──
            ws.Rename("high-value", "top-products");
            Debug.Log($"Renamed: {ws.Has("high-value")}, {ws.Has("top-products")}");

            var backup = ws.Clone("top-products", "top-products-backup");
            Debug.Log($"Cloned: {backup.Name}");

            ws.Remove("top-products-backup");
            Debug.Log($"After remove: {ws.DatasetCount} datasets");

            // ── 7. Clear 不影响 store ──
            ws.Clear();
            Debug.Log($"After clear: {ws.DatasetCount} workspace datasets");
            Debug.Log($"Store still has: {store.Names.Count} datasets"); // inventory 仍在
        }
    }
}
