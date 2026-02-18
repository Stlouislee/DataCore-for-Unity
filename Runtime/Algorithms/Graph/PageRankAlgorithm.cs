using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Graph;

namespace AroAro.DataCore.Algorithms.Graph
{
    /// <summary>
    /// Classic PageRank algorithm for directed graphs.
    /// 
    /// Computes the importance score of each node based on the
    /// link structure of the graph using iterative power-method.
    /// 
    /// Output: a new GraphDataset where each node has a "pagerank" property.
    /// Metrics: iterations, converged (bool), maxDelta.
    /// 
    /// Parameters:
    ///   dampingFactor (double, default 0.85) – probability of following a link
    ///   maxIterations (int, default 100)     – iteration cap
    ///   tolerance     (double, default 1e-6) – convergence threshold
    /// </summary>
    public class PageRankAlgorithm : GraphAlgorithmBase
    {
        public override string Name => "PageRank";
        public override string Description => "Computes PageRank centrality scores for all nodes in a directed graph.";

        public override IReadOnlyList<AlgorithmParameterDescriptor> Parameters { get; } =
            new List<AlgorithmParameterDescriptor>
            {
                new("dampingFactor", "Probability of following a link (0-1)", typeof(double), false, 0.85),
                new("maxIterations", "Maximum number of iterations", typeof(int), false, 100),
                new("tolerance", "Convergence threshold (max delta between iterations)", typeof(double), false, 1e-6),
            };

        protected override AlgorithmResult ExecuteGraph(IGraphDataset input, AlgorithmContext context)
        {
            double damping = context.Get("dampingFactor", 0.85);
            int maxIter = context.Get("maxIterations", 100);
            double tolerance = context.Get("tolerance", 1e-6);

            var nodeIds = input.GetNodeIds().ToList();
            int n = nodeIds.Count;

            if (n == 0)
            {
                return AlgorithmResult.MetricsOnly(Name,
                    new Dictionary<string, object>
                    {
                        ["iterations"] = 0,
                        ["converged"] = true,
                        ["maxDelta"] = 0.0,
                    });
            }

            // Map node IDs to indices for fast lookup
            var idToIndex = new Dictionary<string, int>(n);
            for (int i = 0; i < n; i++)
                idToIndex[nodeIds[i]] = i;

            // Build outgoing adjacency + out-degree arrays
            var outNeighborIndices = new List<int>[n];
            var outDegree = new int[n];
            for (int i = 0; i < n; i++)
            {
                var neighbors = input.GetOutNeighbors(nodeIds[i])
                    .Where(nb => idToIndex.ContainsKey(nb))
                    .Select(nb => idToIndex[nb])
                    .ToList();
                outNeighborIndices[i] = neighbors;
                outDegree[i] = neighbors.Count;
            }

            // Build incoming adjacency for efficient iteration
            var inNeighborIndices = new List<int>[n];
            for (int i = 0; i < n; i++)
                inNeighborIndices[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                foreach (int j in outNeighborIndices[i])
                    inNeighborIndices[j].Add(i);
            }

            // Initialize scores
            double[] scores = new double[n];
            double[] newScores = new double[n];
            double initial = 1.0 / n;
            for (int i = 0; i < n; i++)
                scores[i] = initial;

            // Identify dangling nodes (no outgoing edges)
            var danglingIndices = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (outDegree[i] == 0)
                    danglingIndices.Add(i);
            }

            // Power iteration
            int iterations = 0;
            double maxDelta = 0;
            bool converged = false;

            for (int iter = 0; iter < maxIter; iter++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                // Dangling node contribution
                double danglingSum = 0;
                foreach (int di in danglingIndices)
                    danglingSum += scores[di];

                double base_score = (1.0 - damping + damping * danglingSum) / n;

                // Compute new scores
                for (int i = 0; i < n; i++)
                {
                    double sum = 0;
                    foreach (int j in inNeighborIndices[i])
                    {
                        sum += scores[j] / outDegree[j];
                    }
                    newScores[i] = base_score + damping * sum;
                }

                // Check convergence
                maxDelta = 0;
                for (int i = 0; i < n; i++)
                {
                    double delta = Math.Abs(newScores[i] - scores[i]);
                    if (delta > maxDelta)
                        maxDelta = delta;
                }

                // Swap
                var temp = scores;
                scores = newScores;
                newScores = temp;

                iterations = iter + 1;

                // Report progress
                context.ProgressCallback?.Invoke((float)iterations / maxIter);

                if (maxDelta < tolerance)
                {
                    converged = true;
                    break;
                }
            }

            // Build output graph with pagerank property on each node
            string outputName = ResolveOutputName(input, context, "PageRank");
            var output = new GraphData(outputName);

            for (int i = 0; i < n; i++)
            {
                var existingProps = input.GetNodeProperties(nodeIds[i]);
                var newProps = existingProps != null
                    ? new Dictionary<string, object>(existingProps)
                    : new Dictionary<string, object>();
                newProps["pagerank"] = scores[i];
                output.AddNode(nodeIds[i], newProps);
            }

            // Copy edges
            foreach (var (from, to) in input.GetEdges())
            {
                var edgeProps = input.GetEdgeProperties(from, to);
                output.AddEdge(from, to, edgeProps != null
                    ? new Dictionary<string, object>(edgeProps) : null);
            }

            var metrics = new Dictionary<string, object>
            {
                ["iterations"] = iterations,
                ["converged"] = converged,
                ["maxDelta"] = maxDelta,
                ["nodeCount"] = n,
                ["topNodes"] = GetTopNodes(nodeIds, scores, Math.Min(10, n)),
            };

            return AlgorithmResult.Succeeded(Name, output, metrics);
        }

        private static List<(string Id, double Score)> GetTopNodes(
            List<string> nodeIds, double[] scores, int count)
        {
            return nodeIds
                .Select((id, i) => (Id: id, Score: scores[i]))
                .OrderByDescending(x => x.Score)
                .Take(count)
                .ToList();
        }
    }
}
