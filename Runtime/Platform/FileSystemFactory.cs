using System;
using UnityEngine;

namespace DataCore.Platform
{
    /// <summary>
    /// Factory for creating platform-specific file system implementations
    /// </summary>
    public static class FileSystemFactory
    {
        private static IFileSystem _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Get the platform-specific file system instance
        /// </summary>
        public static IFileSystem GetFileSystem()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = CreatePlatformFileSystem();
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Set a custom file system implementation (for testing or custom platforms)
        /// </summary>
        public static void SetFileSystem(IFileSystem fileSystem)
        {
            lock (_lock)
            {
                _instance = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            }
        }

        private static IFileSystem CreatePlatformFileSystem()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return new StandardFileSystem();
#elif UNITY_IOS || UNITY_ANDROID
            return new MobileFileSystem();
#elif UNITY_WEBGL
            return new WebGLFileSystem();
#else
            // Fallback to standard file system for unknown platforms
            Debug.LogWarning("Using standard file system for unknown platform. Some features may not work correctly.");
            return new StandardFileSystem();
#endif
        }
    }

#if UNITY_IOS || UNITY_ANDROID
    /// <summary>
    /// Mobile platform file system implementation
    /// </summary>
    public class MobileFileSystem : IFileSystem
    {
        public async System.Threading.Tasks.Task<byte[]> ReadAllBytesAsync(string path, System.Threading.CancellationToken cancellationToken = default)
        {
            var fullPath = Path.Combine(Application.persistentDataPath, path);
            return await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
        }

        public async System.Threading.Tasks.Task WriteAllBytesAsync(string path, byte[] data, System.Threading.CancellationToken cancellationToken = default)
        {
            var fullPath = Path.Combine(Application.persistentDataPath, path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            await System.IO.File.WriteAllBytesAsync(fullPath, data, cancellationToken);
        }

        public bool Exists(string path)
        {
            var fullPath = Path.Combine(Application.persistentDataPath, path);
            return System.IO.File.Exists(fullPath);
        }

        public void Delete(string path)
        {
            var fullPath = Path.Combine(Application.persistentDataPath, path);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        public string GetPersistentDataPath()
        {
            return Application.persistentDataPath;
        }

        public string GetTemporaryDataPath()
        {
            return Application.temporaryCachePath;
        }

        public void CreateDirectory(string path)
        {
            var fullPath = Path.Combine(Application.persistentDataPath, path);
            if (!System.IO.Directory.Exists(fullPath))
            {
                System.IO.Directory.CreateDirectory(fullPath);
            }
        }

        public bool DirectoryExists(string path)
        {
            var fullPath = Path.Combine(Application.persistentDataPath, path);
            return System.IO.Directory.Exists(fullPath);
        }

        public FileInfo GetFileInfo(string path)
        {
            var fullPath = Path.Combine(Application.persistentDataPath, path);
            if (!System.IO.File.Exists(fullPath))
            {
                return new FileInfo { Exists = false };
            }

            var fileInfo = new System.IO.FileInfo(fullPath);
            return new FileInfo
            {
                Exists = true,
                Length = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime
            };
        }
    }
#endif

#if UNITY_WEBGL
    /// <summary>
    /// WebGL platform file system implementation (using IndexedDB via JavaScript)
    /// </summary>
    public class WebGLFileSystem : IFileSystem
    {
        // Note: In a real implementation, you would use JavaScript interop
        // to access IndexedDB or localStorage. This is a simplified version.
        
        public async System.Threading.Tasks.Task<byte[]> ReadAllBytesAsync(string path, System.Threading.CancellationToken cancellationToken = default)
        {
            // WebGL implementation would use IndexedDB here
            throw new NotImplementedException("WebGL file system requires JavaScript interop implementation");
        }

        public async System.Threading.Tasks.Task WriteAllBytesAsync(string path, byte[] data, System.Threading.CancellationToken cancellationToken = default)
        {
            // WebGL implementation would use IndexedDB here
            throw new NotImplementedException("WebGL file system requires JavaScript interop implementation");
        }

        public bool Exists(string path)
        {
            // WebGL implementation would check IndexedDB here
            return false;
        }

        public void Delete(string path)
        {
            // WebGL implementation would delete from IndexedDB here
        }

        public string GetPersistentDataPath()
        {
            return "/idbfs/";
        }

        public string GetTemporaryDataPath()
        {
            return "/tmp/";
        }

        public void CreateDirectory(string path)
        {
            // Directories are not explicitly created in IndexedDB
        }

        public bool DirectoryExists(string path)
        {
            return true; // In IndexedDB, directories are implicit
        }

        public FileInfo GetFileInfo(string path)
        {
            return new FileInfo { Exists = false };
        }
    }
#endif
}