using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataCore.Serialization
{
    /// <summary>
    /// Generic serializer interface
    /// </summary>
    public interface ISerializer<T>
    {
        /// <summary>
        /// Serialize data to bytes
        /// </summary>
        byte[] Serialize(T data);
        
        /// <summary>
        /// Deserialize data from bytes
        /// </summary>
        T Deserialize(byte[] bytes);
        
        /// <summary>
        /// Serialize data to file asynchronously
        /// </summary>
        Task SerializeAsync(T data, string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deserialize data from file asynchronously
        /// </summary>
        Task<T> DeserializeAsync(string filePath, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Serialization format options
    /// </summary>
    public enum SerializationFormat
    {
        Binary,
        Json,
        MessagePack,
        Protobuf,
        Numpy,
        Hdf5
    }
    
    /// <summary>
    /// Compression options
    /// </summary>
    public enum CompressionType
    {
        None,
        GZip,
        LZ4
    }
    
    /// <summary>
    /// Serializer configuration
    /// </summary>
    public class SerializerConfig
    {
        public SerializationFormat Format { get; set; } = SerializationFormat.Binary;
        public CompressionType Compression { get; set; } = CompressionType.None;
        public bool IncludeMetadata { get; set; } = true;
        public int BufferSize { get; set; } = 4096;
    }
}