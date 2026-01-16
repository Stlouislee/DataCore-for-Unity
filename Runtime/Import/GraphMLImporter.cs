using System;
using System.Collections.Generic;
using System.IO;
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
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse GraphML: {ex.Message}");
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

            foreach (XmlNode nodeNode in nodeNodes)
            {
                var id = nodeNode.Attributes?["id"]?.Value;
                if (string.IsNullOrEmpty(id)) continue;

                // 如果节点已存在，可能是在多图环境中，或者是无向边提前触发了节点创建
                if (!graph.HasNode(id))
                {
                    var properties = ParseProperties(nodeNode, "node", keyMap, nsmgr);
                    graph.AddNode(id, properties);
                }
            }
        }

        private static void ParseEdges(XmlNode graphNode, IGraphDataset graph, Dictionary<string, KeyDefinition> keyMap, XmlNamespaceManager nsmgr, bool defaultDirected)
        {
            string xpath = string.IsNullOrEmpty(nsmgr.LookupNamespace("g")) ? "edge" : "g:edge";
            var edgeNodes = graphNode.SelectNodes(xpath, nsmgr);
            if (edgeNodes == null) return;

            foreach (XmlNode edgeNode in edgeNodes)
            {
                var source = edgeNode.Attributes?["source"]?.Value;
                var target = edgeNode.Attributes?["target"]?.Value;

                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) continue;

                // 确保节点存在（GraphML 规范中节点通常在边之前定义，但为了健壮性...）
                if (!graph.HasNode(source)) graph.AddNode(source);
                if (!graph.HasNode(target)) graph.AddNode(target);

                string directedAttr = edgeNode.Attributes?["directed"]?.Value;
                bool isDirected = defaultDirected;
                if (directedAttr == "true") isDirected = true;
                else if (directedAttr == "false") isDirected = false;

                var properties = ParseProperties(edgeNode, "edge", keyMap, nsmgr);
                
                try 
                {
                    graph.AddEdge(source, target, properties);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Skipping edge {source}->{target}: {ex.Message}");
                }

                if (!isDirected && source != target)
                {
                    try
                    {
                        if (!graph.HasEdge(target, source))
                            graph.AddEdge(target, source, properties);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Could not add reverse edge {target}->{source}: {ex.Message}");
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