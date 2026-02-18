using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Graph;

namespace AroAro.DataCore.Algorithms.Graph
{
    /// <summary>
    /// Connected Components algorithm using BFS.
    /// Finds all connected components in an undirected view of the graph
    /// (treats directed edges as undirected).
    /// 
    /// Output: a new GraphDataset where each node has a "componentId" (int) property.
    /// Metrics: componentCount, largestComponentSize, componentSizes (Dictionary).
    /// 
    /// Parameters:
    ///   directed (bool, default false) – if true, finds strongly connected components
    ///                                    using Tarjan's algorithm; otherwise treats
    ///                                    edges as undirected (weakly connected).
    /// </summary>
    public class ConnectedComponentsAlgorithm : GraphAlgorithmBase
    {
        public override string Name => "ConnectedComponents";
        public override string Description => "Finds connected components (weakly or strongly connected) in a graph.";

        public override IReadOnlyList<AlgorithmParameterDescriptor> Parameters { get; } =
            new List<AlgorithmParameterDescriptor>
            {
                new("directed", "If true, find strongly connected components (Tarjan's). Otherwise weakly connected (BFS).",
                    typeof(bool), false, false),
            };

        protected override AlgorithmResult ExecuteGraph(IGraphDataset input, AlgorithmContext context)
        {
            bool directed = context.Get("directed", false);

            var nodeIds = input.GetNodeIds().ToList();
            int n = nodeIds.Count;

            if (n == 0)
            {
                return AlgorithmResult.MetricsOnly(Name,
                    new Dictionary<string, object>
                    {
                        ["componentCount"] = 0,
                        ["largestComponentSize"] = 0,
                    });
            }

            var idToIndex = new Dictionary<string, int>(n);
            for (int i = 0; i < n; i++)
                idToIndex[nodeIds[i]] = i;

            int[] componentIds;

            if (directed)
            {
                componentIds = TarjanSCC(input, nodeIds, idToIndex, n, context);
            }
            else
            {
                componentIds = WeaklyConnectedBFS(input, nodeIds, idToIndex, n, context);
            }

            // Count component sizes
            var componentSizes = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                int cid = componentIds[i];
                if (!componentSizes.ContainsKey(cid))
                    componentSizes[cid] = 0;
                componentSizes[cid]++;
            }

            int componentCount = componentSizes.Count;
            int largestSize = componentSizes.Values.Max();

            // Build output graph
            string outputName = ResolveOutputName(input, context, "Components");
            var output = new GraphData(outputName);

            for (int i = 0; i < n; i++)
            {
                var existingProps = input.GetNodeProperties(nodeIds[i]);
                var newProps = existingProps != null
                    ? new Dictionary<string, object>(existingProps)
                    : new Dictionary<string, object>();
                newProps["componentId"] = componentIds[i];
                output.AddNode(nodeIds[i], newProps);
            }

            foreach (var (from, to) in input.GetEdges())
            {
                var edgeProps = input.GetEdgeProperties(from, to);
                output.AddEdge(from, to, edgeProps != null
                    ? new Dictionary<string, object>(edgeProps) : null);
            }

            var metrics = new Dictionary<string, object>
            {
                ["componentCount"] = componentCount,
                ["largestComponentSize"] = largestSize,
                ["componentSizes"] = componentSizes,
                ["nodeCount"] = n,
                ["directed"] = directed,
            };

            return AlgorithmResult.Succeeded(Name, output, metrics);
        }

        #region Weakly Connected (BFS)

        private int[] WeaklyConnectedBFS(
            IGraphDataset graph,
            List<string> nodeIds,
            Dictionary<string, int> idToIndex,
            int n,
            AlgorithmContext context)
        {
            int[] components = new int[n];
            for (int i = 0; i < n; i++) components[i] = -1;

            int currentComponent = 0;
            var queue = new Queue<int>();

            for (int i = 0; i < n; i++)
            {
                if (components[i] >= 0) continue;

                context.CancellationToken.ThrowIfCancellationRequested();

                // BFS from node i
                queue.Enqueue(i);
                components[i] = currentComponent;

                while (queue.Count > 0)
                {
                    int node = queue.Dequeue();
                    string nodeId = nodeIds[node];

                    // Treat edges as undirected: both in and out neighbors
                    foreach (var neighborId in graph.GetNeighbors(nodeId))
                    {
                        if (!idToIndex.TryGetValue(neighborId, out int neighborIdx))
                            continue;
                        if (components[neighborIdx] >= 0) continue;

                        components[neighborIdx] = currentComponent;
                        queue.Enqueue(neighborIdx);
                    }
                }

                currentComponent++;

                context.ProgressCallback?.Invoke((float)i / n);
            }

            return components;
        }

        #endregion

        #region Strongly Connected (Tarjan's)

        private int[] TarjanSCC(
            IGraphDataset graph,
            List<string> nodeIds,
            Dictionary<string, int> idToIndex,
            int n,
            AlgorithmContext context)
        {
            int[] componentIds = new int[n];
            for (int i = 0; i < n; i++) componentIds[i] = -1;

            int[] disc = new int[n];
            int[] low = new int[n];
            bool[] onStack = new bool[n];
            for (int i = 0; i < n; i++)
            {
                disc[i] = -1;
                low[i] = -1;
            }

            var stack = new Stack<int>();
            int time = 0;
            int componentId = 0;

            // Iterative Tarjan's to avoid stack overflow on large graphs
            for (int i = 0; i < n; i++)
            {
                if (disc[i] >= 0) continue;

                context.CancellationToken.ThrowIfCancellationRequested();

                // Iterative DFS using explicit call stack
                var callStack = new Stack<(int Node, IEnumerator<int> Neighbors, bool Initialized)>();
                disc[i] = low[i] = time++;
                onStack[i] = true;
                stack.Push(i);

                var neighbors_i = GetOutNeighborIndices(graph, nodeIds[i], idToIndex).GetEnumerator();
                callStack.Push((i, neighbors_i, true));

                while (callStack.Count > 0)
                {
                    var (node, neighbors, initialized) = callStack.Pop();

                    bool pushedChild = false;
                    while (neighbors.MoveNext())
                    {
                        int w = neighbors.Current;
                        if (disc[w] < 0)
                        {
                            disc[w] = low[w] = time++;
                            onStack[w] = true;
                            stack.Push(w);

                            callStack.Push((node, neighbors, true));
                            var w_neighbors = GetOutNeighborIndices(graph, nodeIds[w], idToIndex).GetEnumerator();
                            callStack.Push((w, w_neighbors, true));
                            pushedChild = true;
                            break;
                        }
                        else if (onStack[w])
                        {
                            low[node] = Math.Min(low[node], disc[w]);
                        }
                    }

                    if (!pushedChild)
                    {
                        // All neighbors processed — update parent's low
                        if (callStack.Count > 0)
                        {
                            var parent = callStack.Peek();
                            low[parent.Node] = Math.Min(low[parent.Node], low[node]);
                        }

                        // Check if this is a root of an SCC
                        if (low[node] == disc[node])
                        {
                            int w;
                            do
                            {
                                w = stack.Pop();
                                onStack[w] = false;
                                componentIds[w] = componentId;
                            } while (w != node);

                            componentId++;
                        }
                    }
                }

                context.ProgressCallback?.Invoke((float)i / n);
            }

            return componentIds;
        }

        private static IEnumerable<int> GetOutNeighborIndices(
            IGraphDataset graph, string nodeId, Dictionary<string, int> idToIndex)
        {
            foreach (var neighbor in graph.GetOutNeighbors(nodeId))
            {
                if (idToIndex.TryGetValue(neighbor, out int idx))
                    yield return idx;
            }
        }

        #endregion
    }
}
