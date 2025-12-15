using System;
using System.IO;

#if UNITY_2019_1_OR_NEWER
using UnityEngine;
#endif

namespace AroAro.DataCore.Persistence
{
    public sealed class FileStorageBackend : IStorageBackend
    {
        public static readonly FileStorageBackend Default = new();

        public byte[] ReadAllBytes(string path)
        {
            var p = Resolve(path);
            return File.ReadAllBytes(p);
        }

        public void WriteAllBytes(string path, byte[] data)
        {
            var p = Resolve(path);
            var dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(p, data);
        }

        private static string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path required", nameof(path));

            // If relative, store under persistentDataPath (Unity) or current directory (non-Unity).
            if (!Path.IsPathRooted(path))
            {
#if UNITY_2019_1_OR_NEWER
                return Path.Combine(Application.persistentDataPath, path);
#else
                return Path.GetFullPath(path);
#endif
            }

            return path;
        }
    }
}
