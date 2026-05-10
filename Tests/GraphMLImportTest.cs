using NUnit.Framework;
using UnityEngine;
using UnityEngine.Assertions;
using System.IO;
using AroAro.DataCore;
using AroAro.DataCore.Import;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// GraphML 导入测试
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// </summary>
    [TestFixture]
    public class GraphMLImportTest
    {
        private static readonly string TestDir = Path.Combine(Path.GetTempPath(), "DataCoreGraphMLTests");

        private static string GetTestDbPath(string name)
        {
            Directory.CreateDirectory(TestDir);
            return Path.Combine(TestDir, name);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(TestDir))
                    Directory.Delete(TestDir, true);
            }
            catch { }
        }

        [Test]
        public void TestGraphMLTextImport()
        {
            Debug.Log("开始 GraphML 文本导入测试...");

            string dbPath = GetTestDbPath("graphml_text_test.db");
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); }
                catch { }
            }

            string graphmlText = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<graphml xmlns=""http://graphml.graphdrawing.org/xmlns"">
    <graph id=""simple"" edgedefault=""directed"">
        <node id=""n1"">
            <data key=""label"">Node 1</data>
        </node>
        <node id=""n2"">
            <data key=""label"">Node 2</data>
        </node>
        <edge source=""n1"" target=""n2"">
            <data key=""weight"">1.0</data>
        </edge>
    </graph>
</graphml>";

            using (var store = new DataCoreStore(dbPath))
            {
                var graph = store.CreateGraph("SimpleGraph");
                GraphMLImporter.ImportToGraph(graphmlText, graph);

                Assert.AreEqual(2, graph.NodeCount, "Should have 2 nodes");
                Assert.AreEqual(1, graph.EdgeCount, "Should have 1 edge");
                Assert.IsTrue(graph.HasNode("n1"), "Node n1 should exist");
                Assert.IsTrue(graph.HasNode("n2"), "Node n2 should exist");
                Assert.IsTrue(graph.HasEdge("n1", "n2"), "Edge n1→n2 should exist");

                var n1Props = graph.GetNodeProperties("n1");
                Assert.IsNotNull(n1Props);
                Assert.AreEqual("Node 1", n1Props["label"]);

                var edgeProps = graph.GetEdgeProperties("n1", "n2");
                Assert.IsNotNull(edgeProps);
                Assert.AreEqual("1.0", edgeProps["weight"].ToString());

                Debug.Log("✅ GraphML text import test passed!");
            }
        }
    }
}
