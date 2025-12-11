using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DataCore.Platform
{
    /// <summary>
    /// Platform-agnostic file system interface for cross-platform data access
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Read all bytes from a file asynchronously
        /// </summary>
        Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Write all bytes to a file asynchronously
        /// </summary>
        Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Check if a file exists
        /// </summary>
        bool Exists(string path);
        
        /// <summary>
        /// Delete a file
        /// </summary>
        void Delete(string path);
        
        /// <summary>
        /// Get the persistent data path for the current platform
        /// </summary>
        string GetPersistentDataPath();
        
        /// <summary>
        /// Get the temporary data path for the current platform
        /// </summary>
        string GetTemporaryDataPath();
        
        /// <summary>
        /// Create a directory if it doesn't exist
        /// </summary>
        void CreateDirectory(string path);
        
        /// <summary>
        /// Check if a directory exists
        /// </summary>
        bool DirectoryExists(string path);
        
        /// <summary>
        /// Get file information
        /// </summary>
        FileInfo GetFileInfo(string path);
    }
    
    /// <summary>
    /// File information structure
    /// </summary>
    public struct FileInfo
    {
        public long Length { get; set; }
        public DateTime LastWriteTime { get; set; }
        public bool Exists { get; set; }
    }
}