using System;
using System.Linq;

namespace AroAro.DataCore
{
    /// <summary>
    /// 原生查询执行异常
    /// </summary>
    public class DataCoreRawQueryException : Exception
    {
        /// <summary>
        /// 原始查询表达式
        /// </summary>
        public string Expression { get; }

        /// <summary>
        /// 查询参数（仅保留引用，不序列化到消息中）
        /// </summary>
        public object[] Parameters { get; }

        /// <summary>
        /// 创建异常实例
        /// </summary>
        public DataCoreRawQueryException(string expression, object[] parameters, string message, Exception innerException)
            : base($"Raw query failed: {message}\nExpression: {expression}\nParameter count: {parameters?.Length ?? 0}", innerException)
        {
            Expression = expression;
            Parameters = parameters;
        }

        /// <summary>
        /// 获取格式化的参数信息（仅用于调试，生产环境慎用）
        /// </summary>
        public string GetFormattedParameters()
        {
            return FormatParameters(Parameters);
        }

        private static string FormatParameters(object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return "[]";

            return "[" + string.Join(", ", parameters.Select((p, i) =>
            {
                if (p == null) return "null";
                var s = p.ToString();
                // 截断超过 50 字符的值，避免日志泄漏
                return s.Length > 50 ? s.Substring(0, 50) + "..." : s;
            })) + "]";
        }
    }
}
