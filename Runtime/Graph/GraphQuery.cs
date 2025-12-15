using System;
using System.Collections.Generic;

namespace AroAro.DataCore.Graph
{
    public sealed class GraphQuery
    {
        private readonly GraphData _graph;
        private readonly List<Func<GraphData.Node, bool>> _nodePredicates = new();

        internal GraphQuery(GraphData graph)
        {
            _graph = graph;
        }

        public GraphQuery WhereNodePropertyEquals(string key, string value)
        {
            _nodePredicates.Add(n => n.Properties.TryGetValue(key, out var v) && string.Equals(v, value, StringComparison.Ordinal));
            return this;
        }

        public string[] ToNodeIds()
        {
            var results = new List<string>();
            foreach (var n in _graph.NodesInternal())
            {
                var ok = true;
                for (var i = 0; i < _nodePredicates.Count; i++)
                {
                    if (!_nodePredicates[i](n))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) results.Add(n.Id);
            }
            return results.ToArray();
        }
    }
}
