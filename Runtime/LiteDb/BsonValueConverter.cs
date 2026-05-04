using System;
using System.Collections.Generic;
using LiteDB;

namespace AroAro.DataCore.LiteDb
{
    /// <summary>
    /// Shared BsonValue conversion utilities for LiteDB datasets.
    /// </summary>
    internal static class BsonValueConverter
    {
        public static BsonValue ToBsonValue(object value)
        {
            if (value == null) return BsonValue.Null;
            return value switch
            {
                double d => new BsonValue(d),
                float f => new BsonValue(f),
                int i => new BsonValue(i),
                long l => new BsonValue(l),
                bool b => new BsonValue(b),
                DateTime dt => new BsonValue(dt),
                string s => new BsonValue(s),
                _ => new BsonValue(value.ToString())
            };
        }

        public static object FromBsonValue(BsonValue value)
        {
            if (value.IsNull) return null;
            if (value.IsDouble) return value.AsDouble;
            if (value.IsInt32) return value.AsInt32;
            if (value.IsInt64) return value.AsInt64;
            if (value.IsBoolean) return value.AsBoolean;
            if (value.IsDateTime) return value.AsDateTime;
            if (value.IsString) return value.AsString;
            return value.ToString();
        }

        public static BsonDocument ToBsonDocument(IDictionary<string, object> values)
        {
            var doc = new BsonDocument();
            if (values != null)
            {
                foreach (var kv in values)
                {
                    doc[kv.Key] = ToBsonValue(kv.Value);
                }
            }
            return doc;
        }

        public static Dictionary<string, object> FromBsonDocument(BsonDocument doc)
        {
            var result = new Dictionary<string, object>();
            foreach (var element in doc)
            {
                result[element.Key] = FromBsonValue(element.Value);
            }
            return result;
        }
    }
}
