using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataCore.Graph;
using Newtonsoft.Json;

namespace DataCore.Serialization
{
    /// <summary>
    /// JSON serializer for graph data
    /// </summary>
    public class GraphJsonSerializer : ISerializer<GraphData>
    {
        private readonly SerializerConfig _config;
        private readonly JsonSerializerSettings _jsonSettings;
        
        public GraphJsonSerializer(SerializerConfig config = null)
        {
            _config = config ?? new SerializerConfig { Format = SerializationFormat.Json };
            
            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto
            };
        }
        
        public byte[] Serialize(GraphData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            var json = JsonConvert.SerializeObject(data, _jsonSettings);
            return Encoding.UTF8.GetBytes(json);
        }
        
        public GraphData Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes cannot be null or empty", nameof(bytes));
            
            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<GraphData>(json, _jsonSettings);
        }
        
        public async Task SerializeAsync(GraphData data, string filePath, CancellationToken cancellationToken = default)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonConvert.SerializeObject(data, _jsonSettings);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
        }
        
        public async Task<GraphData> DeserializeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
            return JsonConvert.DeserializeObject<GraphData>(json, _jsonSettings);
        }
    }
    
    /// <summary>
    /// GraphML format serializer
    /// </summary>
    public class GraphMLSerializer
    {
        public async Task SerializeAsync(GraphData data, string filePath, CancellationToken cancellationToken = default)
        {
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\">");
            xml.AppendLine("  <graph id=\"G\" edgedefault=\"directed\">");
            
            // Write vertices
            foreach (var vertex in data.Vertices)
            {
                xml.AppendLine($"    <node id=\"{vertex.Id}\"/>");
            }
            
            // Write edges
            foreach (var edge in data.Edges)
            {
                xml.AppendLine($"    <edge source=\"{edge.SourceId}\" target=\"{edge.TargetId}\"/>");
            }
            
            xml.AppendLine("  </graph>");
            xml.AppendLine("</graphml>");
            
            await File.WriteAllTextAsync(filePath, xml.ToString(), Encoding.UTF8, cancellationToken);
        }
    }
}