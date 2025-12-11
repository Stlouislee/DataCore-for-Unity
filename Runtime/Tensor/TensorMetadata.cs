using System;
using System.Collections.Generic;

namespace DataCore.Tensor
{
    /// <summary>
    /// Metadata for tensor datasets
    /// </summary>
    [Serializable]
    public class TensorMetadata
    {
        /// <summary>
        /// Unique identifier for the dataset
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedTime { get; set; }
        
        /// <summary>
        /// Last modification timestamp
        /// </summary>
        public DateTime ModifiedTime { get; set; }
        
        /// <summary>
        /// Data shape (dimensions)
        /// </summary>
        public int[] Shape { get; set; }
        
        /// <summary>
        /// Data type (e.g., float32, int32)
        /// </summary>
        public string DataType { get; set; }
        
        /// <summary>
        /// Custom tags for categorization
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
        
        /// <summary>
        /// Description of the dataset
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Version information
        /// </summary>
        public int Version { get; set; } = 1;
        
        /// <summary>
        /// Custom properties dictionary
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// File path if persisted
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// Serialization format used
        /// </summary>
        public string Format { get; set; }
        
        /// <summary>
        /// Checksum for data integrity
        /// </summary>
        public string Checksum { get; set; }
        
        public TensorMetadata()
        {
            CreatedTime = DateTime.UtcNow;
            ModifiedTime = DateTime.UtcNow;
        }
        
        public TensorMetadata(string name) : this()
        {
            Name = name;
        }
        
        /// <summary>
        /// Update modification timestamp
        /// </summary>
        public void Touch()
        {
            ModifiedTime = DateTime.UtcNow;
            Version++;
        }
        
        /// <summary>
        /// Get the total number of elements
        /// </summary>
        public long GetElementCount()
        {
            if (Shape == null || Shape.Length == 0)
                return 0;
                
            long count = 1;
            foreach (var dim in Shape)
            {
                count *= dim;
            }
            return count;
        }
        
        /// <summary>
        /// Get the size in bytes (estimated)
        /// </summary>
        public long GetEstimatedSizeInBytes()
        {
            var elementCount = GetElementCount();
            var elementSize = GetElementSizeInBytes();
            return elementCount * elementSize;
        }
        
        private int GetElementSizeInBytes()
        {
            return DataType?.ToLower() switch
            {
                "float32" or "single" => 4,
                "float64" or "double" => 8,
                "int32" => 4,
                "int64" => 8,
                "int16" => 2,
                "uint32" => 4,
                "uint64" => 8,
                "uint16" => 2,
                "byte" or "uint8" => 1,
                "bool" => 1,
                _ => 4 // Default to 4 bytes
            };
        }
    }
    
    /// <summary>
    /// Version information for tensor datasets
    /// </summary>
    [Serializable]
    public class TensorVersion
    {
        public int VersionNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public string Checksum { get; set; }
        
        public TensorVersion()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
}