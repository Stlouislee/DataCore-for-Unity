using System;
using System.Collections.Generic;
using System.Linq;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// 会话管理器，负责创建、管理和销毁会话
    /// </summary>
    public sealed class SessionManager
    {
        private readonly Dictionary<string, ISession> _sessions = new(StringComparer.Ordinal);
        private readonly DataCoreStore _store;
        private readonly object _lock = new object();

        public SessionManager(DataCoreStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// 获取所有会话ID
        /// </summary>
        public IReadOnlyCollection<string> SessionIds => _sessions.Keys;

        /// <summary>
        /// 创建新会话
        /// </summary>
        /// <param name="name">会话名称</param>
        /// <returns>新创建的会话</returns>
        public ISession CreateSession(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Session name cannot be null or empty", nameof(name));

            lock (_lock)
            {
                var session = new Session(name, _store);
                _sessions[session.Id] = session;
                return session;
            }
        }

        /// <summary>
        /// 获取指定ID的会话
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>会话</returns>
        public ISession GetSession(string sessionId)
        {
            lock (_lock)
            {
                if (!_sessions.TryGetValue(sessionId, out var session))
                    throw new KeyNotFoundException($"Session not found: {sessionId}");

                return session;
            }
        }

        /// <summary>
        /// 检查会话是否存在
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>是否存在</returns>
        public bool HasSession(string sessionId)
        {
            lock (_lock)
            {
                return _sessions.ContainsKey(sessionId);
            }
        }

        /// <summary>
        /// 关闭指定的会话
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>是否成功关闭</returns>
        public bool CloseSession(string sessionId)
        {
            lock (_lock)
            {
                if (!_sessions.TryGetValue(sessionId, out var session))
                    return false;

                session.Dispose();
                _sessions.Remove(sessionId);
                return true;
            }
        }

        /// <summary>
        /// 关闭所有会话
        /// </summary>
        public void CloseAllSessions()
        {
            lock (_lock)
            {
                foreach (var session in _sessions.Values)
                {
                    session.Dispose();
                }
                _sessions.Clear();
            }
        }

        /// <summary>
        /// 清理空闲会话（超过指定时间未活动的会话）
        /// </summary>
        /// <param name="idleTimeout">空闲超时时间</param>
        /// <returns>被清理的会话数量</returns>
        public int CleanupIdleSessions(TimeSpan idleTimeout)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var toRemove = _sessions.Values
                    .Where(s => (now - s.LastActivityAt) > idleTimeout)
                    .ToList();

                foreach (var session in toRemove)
                {
                    session.Dispose();
                    _sessions.Remove(session.Id);
                }

                return toRemove.Count;
            }
        }

        /// <summary>
        /// 获取会话统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public SessionStatistics GetStatistics()
        {
            lock (_lock)
            {
                var sessions = _sessions.Values.ToList();
                return new SessionStatistics
                {
                    TotalSessions = sessions.Count,
                    TotalDatasets = sessions.Sum(s => s.DatasetCount),
                    AverageDatasetsPerSession = sessions.Count > 0 ? sessions.Average(s => s.DatasetCount) : 0
                };
            }
        }
    }

    /// <summary>
    /// 会话统计信息
    /// </summary>
    public class SessionStatistics
    {
        /// <summary>
        /// 总会话数
        /// </summary>
        public int TotalSessions { get; set; }

        /// <summary>
        /// 总数据集数
        /// </summary>
        public int TotalDatasets { get; set; }

        /// <summary>
        /// 平均每会话数据集数
        /// </summary>
        public double AverageDatasetsPerSession { get; set; }
    }
}