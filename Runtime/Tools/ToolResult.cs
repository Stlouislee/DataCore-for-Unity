using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AroAro.DataCore.Tools
{
    /// <summary>
    /// Agent tool 的统一返回结构
    /// </summary>
    public class ToolResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonPropertyName("result")]
        public object Result { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }

        [JsonPropertyName("suggestion")]
        public string Suggestion { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static ToolResult Ok(string action, object result)
        {
            return new ToolResult
            {
                Success = true,
                Action = action,
                Result = result
            };
        }

        public static ToolResult Fail(string action, string error, string suggestion = null)
        {
            return new ToolResult
            {
                Success = false,
                Action = action,
                Error = error,
                Suggestion = suggestion
            };
        }
    }
}
