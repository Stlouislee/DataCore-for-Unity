using NUnit.Framework;
using AroAro.DataCore;
using AroAro.DataCore.Tabular;
using AroAro.DataCore.Graph;
using NumSharp;

namespace AroAro.DataCore.Tests
{
    public sealed class DataCoreSmokeTests
    {
        [Test]
        public void Tabular_Crud_Query_Works()
        {
            var store = new DataCoreStore();
            var t = store.CreateTabular("t");

            t.AddNumericColumn("x", np.array(new double[] { 1, 2, 3 }));
            t.AddStringColumn("s", new[] { "a", "b", "c" });

            t.AddRow(new System.Collections.Generic.Dictionary<string, object>
            {
                ["x"] = 10,
                ["s"] = "z"
            });

            Assert.AreEqual(4, t.RowCount);

            var idx = t.Query().Where("x", TabularOp.Gt, 2).ToRowIndices();
            Assert.AreEqual(2, idx.Length);
            Assert.AreEqual(2, idx[0]);
            Assert.AreEqual(3, idx[1]);

            t.UpdateRow(0, new System.Collections.Generic.Dictionary<string, object> { ["x"] = 99 });
            Assert.AreEqual(99d, (double)t.GetNumericColumn("x")[0]);

            t.DeleteRow(1);
            Assert.AreEqual(3, t.RowCount);
        }

        [Test]
        public void Graph_Crud_Query_Works()
        {
            var store = new DataCoreStore();
            var g = store.CreateGraph("g");

            g.AddNode("a", new System.Collections.Generic.Dictionary<string, string> { ["type"] = "root" });
            g.AddNode("b", new System.Collections.Generic.Dictionary<string, string> { ["type"] = "leaf" });
            g.AddEdge("a", "b");

            Assert.AreEqual(2, g.NodeCount);
            Assert.AreEqual(1, g.EdgeCount);

            var nodes = g.Query().WhereNodePropertyEquals("type", "root").ToNodeIds();
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual("a", nodes[0]);
        }
    }
}
