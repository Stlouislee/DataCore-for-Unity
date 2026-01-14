using NUnit.Framework;
using AroAro.DataCore;
using System.Linq;

namespace AroAro.DataCore.Tests
{
    public sealed class DataCoreSmokeTests
    {
        [Test]
        public void Tabular_Crud_Works()
        {
            using var store = new DataCoreStore();
            var t = store.CreateTabular("test_tabular");

            // 添加列
            t.AddNumericColumn("x", new double[] { 1, 2, 3 });
            t.AddStringColumn("s", new[] { "a", "b", "c" });

            Assert.AreEqual(3, t.RowCount);
            Assert.AreEqual(2, t.ColumnCount);

            // 查询测试
            var results = t.Query()
                .WhereGreaterThan("x", 1.5)
                .ToDictionaries()
                .ToList();

            Assert.AreEqual(2, results.Count);

            // 清理
            store.Delete("test_tabular");
        }

        [Test]
        public void Graph_Crud_Works()
        {
            using var store = new DataCoreStore();
            var g = store.CreateGraph("test_graph");

            // 添加节点
            g.AddNode("a", new System.Collections.Generic.Dictionary<string, object> { ["type"] = "root" });
            g.AddNode("b", new System.Collections.Generic.Dictionary<string, object> { ["type"] = "leaf" });
            g.AddEdge("a", "b");

            Assert.AreEqual(2, g.NodeCount);
            Assert.AreEqual(1, g.EdgeCount);

            // 获取邻居
            var neighbors = g.GetNeighbors("a").ToList();
            Assert.AreEqual(1, neighbors.Count);
            Assert.AreEqual("b", neighbors[0]);

            // 清理
            store.Delete("test_graph");
        }

        [Test]
        public void Store_Operations_Work()
        {
            using var store = new DataCoreStore();
            
            // 创建数据集
            store.CreateTabular("t1");
            store.CreateGraph("g1");

            // 检查存在
            Assert.IsTrue(store.HasDataset("t1"));
            Assert.IsTrue(store.HasDataset("g1"));
            Assert.IsFalse(store.HasDataset("notexist"));

            // 获取数据集
            var t = store.GetTabular("t1");
            Assert.IsNotNull(t);

            var g = store.GetGraph("g1");
            Assert.IsNotNull(g);

            // 删除
            Assert.IsTrue(store.Delete("t1"));
            Assert.IsFalse(store.HasDataset("t1"));

            store.Delete("g1");
        }
    }
}
