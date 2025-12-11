using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DataCore.Platform
{
    /// <summary>
    /// Standard file system implementation for Windows, macOS, and Linux
    /// </summary>
    public class StandardFileSystem : IFileSystem
    {
        public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
                
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                var buffer = new byte[fileStream.Length];
                await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                return buffer;
            }
        }

        public async Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await fileStream.WriteAsync(data, 0, data.Length, cancellationToken);
            }
        }

        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public string GetPersistentDataPath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dataPath = Path.Combine(appDataPath, "DataCoreForUnity");
            
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            
            return dataPath;
        }

        public string GetTemporaryDataPath()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "DataCoreForUnity");
            
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }
            
            return tempPath;
        }

        public void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public FileInfo GetFileInfo(string path)
        {
            if (!File.Exists(path))
            {
                return new FileInfo { Exists = false };
            }

            var fileInfo = new System.IO.FileInfo(path);
            return new FileInfo
            {
                Exists = true,
                Length = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime
            };
        }
    }
}