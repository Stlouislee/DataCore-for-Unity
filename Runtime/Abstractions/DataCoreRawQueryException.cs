using System;
using System.Linq;
using LiteDB;

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
        /// 查询参数
        /// </summary>
        public object[] Parameters { get; }
        
        /// <summary>
        /// 创建异常实例
        /// </summary>
        public DataCoreRawQueryException(string expression, object[] parameters, string message, Exception innerException)
            : base($"Raw query failed: {message}\nExpression: {expression}\nParameters: {FormatParameters(parameters)}", innerException)
        {
            Expression = expression;
            Parameters = parameters;
        }
        
        private static string FormatParameters(object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return "[]";
            
            return "[" + string.Join(", ", parameters.Select(p => p?.ToString() ?? "null")) + "]";
        }
    }
}