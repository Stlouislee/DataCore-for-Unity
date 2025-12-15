using System;

namespace AroAro.DataCore.Persistence
{
    public interface IStorageBackend
    {
        byte[] ReadAllBytes(string path);
        void WriteAllBytes(string path, byte[] data);
    }
}
