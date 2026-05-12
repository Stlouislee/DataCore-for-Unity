using System;
using System.IO;

#if UNITY_2019_1_OR_NEWER
using UnityEngine;
#endif

namespace AroAro.DataCore
{
    /// <summary>
    /// 数据存储工厂 - 创建 LiteDB 存储实现
    /// </summary>
    public static class DataStoreFactory
    {
        /// <summary>
        /// 创建数据存储实例
        /// </summary>
        /// <param name="path">数据库文件路径</param>
        /// <param name="options">可选配置</param>
        public static IDataStore Create(string path = null, DataStoreOptions options = null)
        {
            path ??= GetDefaultPath();
            options ??= new DataStoreOptions();
            return new LiteDb.LiteDbDataStore(path, options);
        }

        /// <summary>
        /// 创建 LiteDB 存储（主要入口）
        /// </summary>
        /// <param name="path">数据库文件路径，默认为 DataCore/datacore.db</param>
        /// <param name="options">可选配置</param>
        public static IDataStore CreateLiteDb(string path = null, DataStoreOptions options = null)
        {
            return Create(path, options);
        }

        /// <summary>
        /// 获取默认数据库路径
        /// </summary>
        public static string GetDefaultPath()
        {
#if UNITY_2019_1_OR_NEWER
            return Path.Combine(Application.persistentDataPath, "DataCore", "datacore.db");
#else
            return "DataCore/datacore.db";
#endif
        }
    }

    /// <summary>
    /// 数据存储配置选项
    /// </summary>
    public class DataStoreOptions
    {
        /// <summary>
        /// 是否只读模式
        /// </summary>
        public bool ReadOnly { get; set; } = false;

        /// <summary>
        /// 是否在启动时自动创建索引
        /// </summary>
        [Obsolete("Not used by current backend. Will be removed in v1.0.")]
        public bool AutoCreateIndexes { get; set; } = true;

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        [Obsolete("Not used by current backend. Will be removed in v1.0.")]
        public bool EnableCache { get; set; } = true;

        /// <summary>
        /// 缓存大小（项数）
        /// </summary>
        [Obsolete("Not used by current backend. Will be removed in v1.0.")]
        public int CacheSize { get; set; } = 1000;

        /// <summary>
        /// 是否自动保存
        /// </summary>
        [Obsolete("Not used by current backend. Will be removed in v1.0.")]
        public bool AutoSave { get; set; } = true;

        /// <summary>
        /// 自动保存间隔（秒）
        /// </summary>
        [Obsolete("Not used by current backend. Will be removed in v1.0.")]
        public int AutoSaveInterval { get; set; } = 60;

        /// <summary>
        /// 连接字符串（用于高级配置）
        /// </summary>
        [Obsolete("Not used by current backend. Will be removed in v1.0.")]
        public string ConnectionString { get; set; }
    }
}
