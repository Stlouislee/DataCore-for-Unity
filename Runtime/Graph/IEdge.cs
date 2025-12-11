using System.Collections.Generic;

namespace DataCore.Graph
{
    /// <summary>
    /// Interface for graph edges
    /// </summary>
    public interface IEdge<TVertex>
    {
        /// <summary>
        /// Source vertex
        /// </summary>
        TVertex Source { get; }
        
        /// <summary>
        /// Target vertex
        /// </summary>
        TVertex Target { get; }
        
        /// <summary>
        /// Edge weight (optional)
        /// </summary>
        double Weight { get; set; }
        
        /// <summary>
        /// Edge properties
        /// </summary>
        Dictionary<string, object> Properties { get; }
        
        /// <summary>
        /// Check if this edge connects the given vertices
        /// </summary>
        bool Connects(TVertex source, TVertex target);
    }
    
    /// <summary>
    /// Basic edge implementation
    /// </summary>
    public class Edge<TVertex> : IEdge<TVertex>
    {
        public TVertex Source { get; }
        public TVertex Target { get; }
        public double Weight { get; set; }
        public Dictionary<string, object> Properties { get; }
        
        public Edge(TVertex source, TVertex target, double weight = 1.0)
        {
            Source = source;
            Target = target;
            Weight = weight;
            Properties = new Dictionary<string, object>();
        }
        
        public bool Connects(TVertex source, TVertex target)
        {
            return Source.Equals(source) && Target.Equals(target);
        }
        
        public override string ToString()
        {
            return $"{Source} -> {Target} (weight: {Weight})";
        }
    }
    
    /// <summary>
    /// Undirected edge implementation
    /// </summary>
    public class UndirectedEdge<TVertex> : IEdge<TVertex>
    {
        public TVertex Source { get; }
        public TVertex Target { get; }
        public double Weight { get; set; }
        public Dictionary<string, object> Properties { get; }
        
        public UndirectedEdge(TVertex vertex1, TVertex vertex2, double weight = 1.0)
        {
            // Store vertices in sorted order for consistent comparison
            var comparer = Comparer<TVertex>.Default;
            if (comparer.Compare(vertex1, vertex2) <= 0)
            {
                Source = vertex1;
                Target = vertex2;
            }
            else
            {
                Source = vertex2;
                Target = vertex1;
            }
            Weight = weight;
            Properties = new Dictionary<string, object>();
        }
        
        public bool Connects(TVertex source, TVertex target)
        {
            return (Source.Equals(source) && Target.Equals(target)) ||
                   (Source.Equals(target) && Target.Equals(source));
        }
        
        public override string ToString()
        {
            return $"{Source} <-> {Target} (weight: {Weight})";
        }
    }
}