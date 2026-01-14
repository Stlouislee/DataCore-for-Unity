using System;
using System.Collections.Generic;
using System.IO;

#if UNITY_2019_1_OR_NEWER
using UnityEngine;
#endif

namespace AroAro.DataCore
{
    /// <summary>
    /// 数据存储工厂 - 创建具体的存储实现
    /// </summary>
    public static class DataStoreFactory
    {
        private static readonly Dictionary<StorageBackend, Func<string, DataStoreOptions, IDataStore>> _factories = new();

        static DataStoreFactory()
        {
            // 注册默认的工厂
            RegisterFactory(StorageBackend.LiteDb, CreateLiteDbStore);
            RegisterFactory(StorageBackend.Memory, CreateMemoryStore);
        }

        /// <summary>
        /// 创建数据存储实例
        /// </summary>
        /// <param name="backend">存储后端类型</param>
        /// <param name="path">存储路径（对于 LiteDb 是数据库文件路径，对于 File 是目录路径）</param>
        /// <param name="options">可选配置</param>
        public static IDataStore Create(StorageBackend backend = StorageBackend.LiteDb, string path = null, DataStoreOptions options = null)
        {
            if (!_factories.TryGetValue(backend, out var factory))
            {
                throw new NotSupportedException($"Storage backend '{backend}' is not supported or not registered");
            }

            path ??= GetDefaultPath(backend);
            options ??= new DataStoreOptions();

            return factory(path, options);
        }

        /// <summary>
        /// 创建 LiteDB 存储
        /// </summary>
        public static IDataStore CreateLiteDb(string path = null)
        {
            return Create(StorageBackend.LiteDb, path);
        }

        /// <summary>
        /// 创建内存存储
        /// </summary>
        public static IDataStore CreateMemory()
        {
            return Create(StorageBackend.Memory);
        }

        /// <summary>
        /// 注册自定义存储工厂
        /// </summary>
        public static void RegisterFactory(StorageBackend backend, Func<string, DataStoreOptions, IDataStore> factory)
        {
            _factories[backend] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// 检查后端是否已注册
        /// </summary>
        public static bool IsBackendRegistered(StorageBackend backend)
        {
            return _factories.ContainsKey(backend);
        }

        #region 内部工厂方法

        private static IDataStore CreateLiteDbStore(string path, DataStoreOptions options)
        {
            // 使用反射加载 LiteDb 实现，避免硬编码依赖
            var type = Type.GetType("AroAro.DataCore.LiteDb.LiteDbDataStore, AroAro.DataCore");
            if (type == null)
            {
                // 直接引用（如果在同一程序集）
                return new LiteDb.LiteDbDataStore(path, options);
            }
            return (IDataStore)Activator.CreateInstance(type, path, options);
        }

        private static IDataStore CreateMemoryStore(string path, DataStoreOptions options)
        {
            return new Memory.MemoryDataStore(options);
        }

        private static string GetDefaultPath(StorageBackend backend)
        {
            return backend switch
            {
                StorageBackend.LiteDb => "DataCore/datacore.db",
                StorageBackend.File => "DataCore/",
                StorageBackend.Memory => null,
                _ => null
            };
        }

        #endregion
    }

    /// <summary>
    /// 数据存储配置选项
    /// </summary>
    public class DataStoreOptions
    {
        /// <summary>
        /// 是否在启动时自动创建索引
        /// </summary>
        public bool AutoCreateIndexes { get; set; } = true;

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCache { get; set; } = true;

        /// <summary>
        /// 缓存大小（项数）
        /// </summary>
        public int CacheSize { get; set; } = 1000;

        /// <summary>
        /// 是否自动保存
        /// </summary>
        public bool AutoSave { get; set; } = true;

        /// <summary>
        /// 自动保存间隔（秒）
        /// </summary>
        public int AutoSaveInterval { get; set; } = 60;

        /// <summary>
        /// 连接字符串（用于高级配置）
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// 是否只读模式
        /// </summary>
        public bool ReadOnly { get; set; } = false;
    }
}
