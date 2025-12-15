using System;
using System.Collections.Generic;
using System.Text;
using AroAro.DataCore.Graph;

namespace AroAro.DataCore.Persistence
{
    public static class GraphJsonSerializer
    {
        public static byte[] Serialize(GraphData graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            // Unity-friendly, dependency-free line format.
            //
            // dcgraph/1
            // name\t<name>
            // N\t<id>\t<k>=<v>...
            // E\t<from>\t<to>\t<k>=<v>...
            var sb = new StringBuilder();
            sb.AppendLine("dcgraph/1");
            sb.Append("name\t").Append(Escape(graph.Name)).Append('\n');

            foreach (var n in graph.NodesInternal())
            {
                sb.Append('N').Append('\t').Append(Escape(n.Id));
                foreach (var kv in n.Properties)
                {
                    sb.Append('\t').Append(Escape(kv.Key)).Append('=').Append(Escape(kv.Value));
                }
                sb.Append('\n');
            }

            foreach (var e in graph.EdgesInternal())
            {
                sb.Append('E').Append('\t').Append(Escape(e.From)).Append('\t').Append(Escape(e.To));
                foreach (var kv in e.Properties)
                {
                    sb.Append('\t').Append(Escape(kv.Key)).Append('=').Append(Escape(kv.Value));
                }
                sb.Append('\n');
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static GraphData Deserialize(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var text = Encoding.UTF8.GetString(bytes);
            var lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != "dcgraph/1")
                throw new InvalidOperationException("Invalid dcgraph header");

            string name = "graph";
            var nodeLines = new List<string>();
            var edgeLines = new List<string>();

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('\t');
                if (parts.Length == 0) continue;

                if (parts[0] == "name" && parts.Length >= 2)
                {
                    name = Unescape(parts[1]);
                    continue;
                }

                if (parts[0] == "N") nodeLines.Add(line);
                else if (parts[0] == "E") edgeLines.Add(line);
            }

            var g = new GraphData(name);

            for (var i = 0; i < nodeLines.Count; i++)
            {
                var parts = nodeLines[i].Split('\t');
                if (parts.Length < 2) continue;
                var id = Unescape(parts[1]);
                var props = ParseProps(parts, startAt: 2);
                g.AddNode(id, props);
            }

            for (var i = 0; i < edgeLines.Count; i++)
            {
                var parts = edgeLines[i].Split('\t');
                if (parts.Length < 3) continue;
                var from = Unescape(parts[1]);
                var to = Unescape(parts[2]);
                var props = ParseProps(parts, startAt: 3);
                g.AddEdge(from, to, props);
            }

            return g;
        }

        private static Dictionary<string, string> ParseProps(string[] parts, int startAt)
        {
            var props = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = startAt; i < parts.Length; i++)
            {
                var token = parts[i];
                if (string.IsNullOrEmpty(token)) continue;
                var eq = token.IndexOf('=');
                if (eq <= 0) continue;
                var k = Unescape(token.Substring(0, eq));
                var v = Unescape(token.Substring(eq + 1));
                props[k] = v;
            }
            return props;
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s
                .Replace("\\", "\\\\")
                .Replace("\t", "\\t")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        private static string Unescape(string s)
        {
            if (s == null) return null;
            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c != '\\' || i == s.Length - 1)
                {
                    sb.Append(c);
                    continue;
                }

                var n = s[++i];
                sb.Append(n switch
                {
                    't' => '\t',
                    'n' => '\n',
                    'r' => '\r',
                    '\\' => '\\',
                    _ => n
                });
            }
            return sb.ToString();
        }
    }
}
