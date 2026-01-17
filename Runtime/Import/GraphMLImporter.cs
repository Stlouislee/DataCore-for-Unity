using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;

namespace AroAro.DataCore.Import
{
    /// <summary>
    /// Utility for importing GraphML data into IGraphDataset
    /// </summary>
    public static class GraphMLImporter
    {
        private class KeyDefinition
        {
            public string Id;
            public string Name;
            public string For; // node, edge, graph, etc.
            public string Type; // string, int, double, long, float, boolean
            public object DefaultValue;
        }

        /// <summary>
        /// 解析 GraphML 文本并导入到现有的图数据集中
        /// </summary>
        /// <param name="graphmlText">GraphML 文本内容</param>
        /// <param name="graph">目标图数据集</param>
        public static void ImportToGraph(string graphmlText, IGraphDataset graph)
        {
            if (string.IsNullOrWhiteSpace(graphmlText))
            {
                Debug.LogError("GraphML text is empty");
                return;
            }

            try
            {
                using (var reader = new StringReader(graphmlText))
                using (var xmlReader = XmlReader.Create(reader))
                {
                    var doc = new XmlDocument();
                    doc.Load(xmlReader);

                    // 解析 GraphML 文档
                    ParseGraphMLDocument(doc, graph);
                    
                    // Flush metadata after bulk import
                    if (graph is LiteDb.LiteDbGraphDataset liteDbGraph)
                    {
                        liteDbGraph.FlushMetadata();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Debug.LogError("Failed to parse GraphML: Database was disposed during import. Try restarting Unity.");
                throw;
            }
            catch (LiteDB.LiteException ex) when (ex.Message.Contains("PAGE_SIZE"))
            {
                Debug.LogError($"Failed to parse GraphML: Database corruption detected ({ex.Message}). Try deleting the database file and reimporting.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse GraphML: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 从 GraphML 文件解析数据并创建新的图数据集
        /// </summary>
        /// <param name="store">数据存储</param>
        /// <param name="graphmlPath">GraphML 文件路径</param>
        /// <param name="datasetName">数据集名称</param>
        /// <returns>创建的图数据集</returns>
        public static IGraphDataset ImportFromFile(IDataStore store, string graphmlPath, string datasetName)
        {
            if (!File.Exists(graphmlPath))
            {
                Debug.LogError($"GraphML file not found: {graphmlPath}");
                return null;
            }

            try
            {
                var graphmlText = File.ReadAllText(graphmlPath);
                var graph = store.CreateGraph(datasetName);
                ImportToGraph(graphmlText, graph);
                return graph;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to import GraphML from file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析 GraphML 文档
        /// </summary>
        private static void ParseGraphMLDocument(XmlDocument doc, IGraphDataset graph)
        {
            var graphmlNode = doc.DocumentElement;
            if (graphmlNode == null || (graphmlNode.LocalName != "graphml" && graphmlNode.Name != "graphml"))
            {
                Debug.LogError("Invalid GraphML document: root element must be 'graphml'");
                return;
            }

            // 1. 解析所有 Key 定义
            var keyMap = ParseKeys(graphmlNode);

            // 查找 graph 元素 (支持带命名空间的查找)
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            if (!string.IsNullOrEmpty(graphmlNode.NamespaceURI))
                nsmgr.AddNamespace("g", graphmlNode.NamespaceURI);

            string xpath = string.IsNullOrEmpty(graphmlNode.NamespaceURI) ? "graph" : "g:graph";
            var graphNodes = graphmlNode.SelectNodes(xpath, nsmgr);
            
            if (graphNodes == null || graphNodes.Count == 0)
            {
                Debug.LogError("No graph element found in GraphML document");
                return;
            }

            // 2. 解析每个图（GraphML 支持多个图，虽然通常只有一个）
            foreach (XmlNode graphNode in graphNodes)
            {
                string edgeDefaultStr = graphNode.Attributes?["edgedefault"]?.Value;
                bool defaultDirected = edgeDefaultStr != "undirected"; 

                ParseNodes(graphNode, graph, keyMap, nsmgr);
                ParseEdges(graphNode, graph, keyMap, nsmgr, defaultDirected);
            }
        }

        private static Dictionary<string, KeyDefinition> ParseKeys(XmlElement root)
        {
            var keyMap = new Dictionary<string, KeyDefinition>();
            var nsmgr = new XmlNamespaceManager(root.OwnerDocument.NameTable);
            if (!string.IsNullOrEmpty(root.NamespaceURI))
                nsmgr.AddNamespace("g", root.NamespaceURI);

            string xpath = string.IsNullOrEmpty(root.NamespaceURI) ? "key" : "g:key";
            var keyNodes = root.SelectNodes(xpath, nsmgr);
            
            if (keyNodes != null)
            {
                foreach (XmlNode node in keyNodes)
                {
                    var id = node.Attributes?["id"]?.Value;
                    if (string.IsNullOrEmpty(id)) continue;

                    var def = new KeyDefinition
                    {
                        Id = id,
                        Name = node.Attributes?["attr.name"]?.Value ?? id,
                        For = node.Attributes?["for"]?.Value ?? "all",
                        Type = node.Attributes?["attr.type"]?.Value ?? "string"
                    };

                    string defXpath = string.IsNullOrEmpty(root.NamespaceURI) ? "default" : "g:default";
                    var defaultNode = node.SelectSingleNode(defXpath, nsmgr);
                    if (defaultNode != null)
                    {
                        def.DefaultValue = ConvertValue(defaultNode.InnerText, def.Type);
                    }

                    keyMap[id] = def;
                }
            }
            return keyMap;
        }

        private static void ParseNodes(XmlNode graphNode, IGraphDataset graph, Dictionary<string, KeyDefinition> keyMap, XmlNamespaceManager nsmgr)
        {
            string xpath = string.IsNullOrEmpty(nsmgr.LookupNamespace("g")) ? "node" : "g:node";
            var nodeNodes = graphNode.SelectNodes(xpath, nsmgr);
            if (nodeNodes == null) return;

            // Collect all nodes first for bulk insertion
            var nodesToAdd = new List<(string Id, IDictionary<string, object> Properties)>();
            
            foreach (XmlNode nodeNode in nodeNodes)
            {
                var id = nodeNode.Attributes?["id"]?.Value;
                if (string.IsNullOrEmpty(id)) continue;

                // 如果节点已存在，可能是在多图环境中，或者是无向边提前触发了节点创建
                if (!graph.HasNode(id))
                {
                    var properties = ParseProperties(nodeNode, "node", keyMap, nsmgr);
                    nodesToAdd.Add((id, properties));
                }
            }
            
            // Bulk add nodes for better performance
            if (nodesToAdd.Count > 0)
            {
                try
                {
                    graph.AddNodes(nodesToAdd);
                    Debug.Log($"Added {nodesToAdd.Count} nodes in bulk");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Bulk node addition failed ({ex.Message}), falling back to individual adds");
                    // Fallback to individual adds
                    foreach (var node in nodesToAdd)
                    {
                        try
                        {
                            graph.AddNode(node.Id, node.Properties);
                        }
                        catch (Exception addEx)
                        {
                            Debug.LogWarning($"Failed to add node {node.Id}: {addEx.Message}");
                        }
                    }
                }
            }
        }

        private static void ParseEdges(XmlNode graphNode, IGraphDataset graph, Dictionary<string, KeyDefinition> keyMap, XmlNamespaceManager nsmgr, bool defaultDirected)
        {
            string xpath = string.IsNullOrEmpty(nsmgr.LookupNamespace("g")) ? "edge" : "g:edge";
            var edgeNodes = graphNode.SelectNodes(xpath, nsmgr);
            if (edgeNodes == null) return;

            // Collect all edges first for bulk insertion
            var edgesToAdd = new List<(string From, string To, IDictionary<string, object> Properties)>();
            var missingNodes = new HashSet<string>();

            foreach (XmlNode edgeNode in edgeNodes)
            {
                var source = edgeNode.Attributes?["source"]?.Value;
                var target = edgeNode.Attributes?["target"]?.Value;

                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) continue;

                // 确保节点存在（GraphML 规范中节点通常在边之前定义，但为了健壮性...）
                if (!graph.HasNode(source)) missingNodes.Add(source);
                if (!graph.HasNode(target)) missingNodes.Add(target);

                string directedAttr = edgeNode.Attributes?["directed"]?.Value;
                bool isDirected = defaultDirected;
                if (directedAttr == "true") isDirected = true;
                else if (directedAttr == "false") isDirected = false;

                var properties = ParseProperties(edgeNode, "edge", keyMap, nsmgr);
                
                edgesToAdd.Add((source, target, properties));

                // Add reverse edge for undirected graphs
                if (!isDirected && source != target)
                {
                    edgesToAdd.Add((target, source, properties));
                }
            }
            
            // Add missing nodes first
            if (missingNodes.Count > 0)
            {
                var nodesToAdd = missingNodes.Select(id => (id, (IDictionary<string, object>)null));
                try
                {
                    graph.AddNodes(nodesToAdd);
                    Debug.Log($"Added {missingNodes.Count} missing nodes");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to add missing nodes in bulk: {ex.Message}");
                    foreach (var nodeId in missingNodes)
                    {
                        try { graph.AddNode(nodeId); }
                        catch { /* ignore */ }
                    }
                }
            }
            
            // Bulk add edges for better performance
            if (edgesToAdd.Count > 0)
            {
                try
                {
                    graph.AddEdges(edgesToAdd);
                    Debug.Log($"Added {edgesToAdd.Count} edges in bulk");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Bulk edge addition failed ({ex.Message}), falling back to individual adds");
                    // Fallback to individual adds
                    foreach (var edge in edgesToAdd)
                    {
                        try
                        {
                            if (!graph.HasEdge(edge.From, edge.To))
                            {
                                graph.AddEdge(edge.From, edge.To, edge.Properties);
                            }
                        }
                        catch (Exception addEx)
                        {
                            Debug.LogWarning($"Skipping edge {edge.From}->{edge.To}: {addEx.Message}");
                        }
                    }
                }
            }
        }

        private static Dictionary<string, object> ParseProperties(XmlNode node, string scope, Dictionary<string, KeyDefinition> keyMap, XmlNamespaceManager nsmgr)
        {
            var properties = new Dictionary<string, object>();
            
            // 填充默认值
            foreach (var kvp in keyMap)
            {
                if ((kvp.Value.For == "all" || kvp.Value.For == scope) && kvp.Value.DefaultValue != null)
                {
                    properties[kvp.Value.Name] = kvp.Value.DefaultValue;
                }
            }

            string xpath = string.IsNullOrEmpty(nsmgr.LookupNamespace("g")) ? "data" : "g:data";
            var dataNodes = node.SelectNodes(xpath, nsmgr);
            if (dataNodes != null)
            {
                foreach (XmlNode dataNode in dataNodes)
                {
                    var keyId = dataNode.Attributes?["key"]?.Value;
                    if (string.IsNullOrEmpty(keyId) || !keyMap.TryGetValue(keyId, out var def)) continue;

                    var value = dataNode.InnerText?.Trim();
                    properties[def.Name] = ConvertValue(value, def.Type);
                }
            }

            return properties;
        }

        private static object ConvertValue(string value, string type)
        {
            if (string.IsNullOrEmpty(value)) return null;

            try
            {
                switch (type.ToLower())
                {
                    case "boolean": return bool.Parse(value);
                    case "int": return int.Parse(value);
                    case "long": return long.Parse(value);
                    case "float": return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    case "double": return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    case "string": return value;
                    default: return value;
                }
            }
            catch
            {
                return value;
            }
        }
    }
}