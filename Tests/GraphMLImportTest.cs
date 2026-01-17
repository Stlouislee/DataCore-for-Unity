using UnityEngine;
using System.IO;
using AroAro.DataCore;
using AroAro.DataCore.Import;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// GraphML 导入测试
    /// </summary>
    public class GraphMLImportTest : MonoBehaviour
    {
        [Header("测试配置")]
        [SerializeField] private string graphmlFilePath = "TestGraphML.graphml";
        [SerializeField] private string datasetName = "TestGraphML";
        
        [Header("测试结果")]
        [SerializeField] private int importedNodeCount;
        [SerializeField] private int importedEdgeCount;
        [SerializeField] private bool importSuccess;
        
        /// <summary>
        /// 运行 GraphML 导入测试
        /// </summary>
        [ContextMenu("Run GraphML Import Test")]
        public void RunGraphMLImportTest()
        {
            Debug.Log("开始 GraphML 导入测试...");
            
            // Ensure clean state
            string dbPath = "graphml_test.db";
            if (File.Exists(dbPath))
            {
                try 
                { 
                    File.Delete(dbPath); 
                    Debug.Log("已清理旧的测试数据库");
                }
                catch (System.Exception ex) 
                { 
                    Debug.LogWarning($"无法删除测试数据库: {ex.Message}"); 
                }
            }

            // 检查文件是否存在
            if (!File.Exists(graphmlFilePath))
            {
                Debug.LogError($"GraphML 文件不存在: {graphmlFilePath}");
                importSuccess = false;
                return;
            }
            
            try
            {
                // 创建数据存储
                using (var store = new DataCoreStore("graphml_test.db"))
                {
                    // 导入 GraphML
                    var graph = GraphMLImporter.ImportFromFile(store.UnderlyingStore, graphmlFilePath, datasetName);
                    
                    if (graph == null)
                    {
                        Debug.LogError("GraphML 导入失败");
                        importSuccess = false;
                        return;
                    }
                    
                    // 记录导入结果
                    importedNodeCount = graph.NodeCount;
                    importedEdgeCount = graph.EdgeCount;
                    importSuccess = true;
                    
                    Debug.Log($"GraphML 导入成功!");
                    Debug.Log($"- 数据集名称: {datasetName}");
                    Debug.Log($"- 导入节点数: {importedNodeCount}");
                    Debug.Log($"- 导入边数: {importedEdgeCount}");
                    
                    // 验证节点属性
                    Debug.Log("节点属性验证:");
                    var nodeIds = graph.GetNodeIds();
                    foreach (var nodeId in nodeIds)
                    {
                        var properties = graph.GetNodeProperties(nodeId);
                        Debug.Log($"  - 节点 {nodeId}: {properties.Count} 个属性");
                        foreach (var prop in properties)
                        {
                            Debug.Log($"    - {prop.Key}: {prop.Value}");
                        }
                    }
                    
                    // 验证边属性
                    Debug.Log("边属性验证:");
                    var edges = graph.GetEdges();
                    foreach (var edge in edges)
                    {
                        var properties = graph.GetEdgeProperties(edge.From, edge.To);
                        Debug.Log($"  - 边 {edge.From} → {edge.To}: {properties.Count} 个属性");
                        foreach (var prop in properties)
                        {
                            Debug.Log($"    - {prop.Key}: {prop.Value}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GraphML 导入测试失败: {ex.Message}");
                Debug.LogError(ex.StackTrace);
                importSuccess = false;
            }
        }
        
        /// <summary>
        /// 测试 GraphML 文本导入
        /// </summary>
        [ContextMenu("Test GraphML Text Import")]
        public void TestGraphMLTextImport()
        {
            Debug.Log("开始 GraphML 文本导入测试...");
            
            // Ensure clean state
            string dbPath = "graphml_text_test.db";
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } 
                catch {}
            }

            // 简单的 GraphML 示例
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
            
            try
            {
                // 创建数据存储
                using (var store = new DataCoreStore("graphml_text_test.db"))
                {
                    var graph = store.CreateGraph("SimpleGraph");
                    
                    // 导入 GraphML 文本
                    GraphMLImporter.ImportToGraph(graphmlText, graph);
                    
                    Debug.Log($"GraphML 文本导入成功!");
                    Debug.Log($"- 节点数: {graph.NodeCount}");
                    Debug.Log($"- 边数: {graph.EdgeCount}");
                    
                    // 验证数据
                    Debug.Log("验证导入的数据:");
                    Debug.Log($"- 节点 n1 存在: {graph.HasNode("n1")}");
                    Debug.Log($"- 节点 n2 存在: {graph.HasNode("n2")}");
                    Debug.Log($"- 边 n1→n2 存在: {graph.HasEdge("n1", "n2")}");
                    
                    var n1Props = graph.GetNodeProperties("n1");
                    Debug.Log($"- 节点 n1 属性 label: {n1Props["label"]}");
                    
                    var edgeProps = graph.GetEdgeProperties("n1", "n2");
                    Debug.Log($"- 边 n1→n2 属性 weight: {edgeProps["weight"]}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GraphML 文本导入测试失败: {ex.Message}");
                Debug.LogError(ex.StackTrace);
            }
        }
    }
}