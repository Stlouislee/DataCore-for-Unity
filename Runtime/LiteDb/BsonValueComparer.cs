using System;
using LiteDB;

namespace AroAro.DataCore.LiteDb
{
    /// <summary>
    /// Shared BsonValue comparison utilities for LiteDB queries.
    /// </summary>
    internal static class BsonValueComparer
    {
        public static bool Evaluate(BsonValue bsonValue, QueryOp op, object value)
        {
            switch (op)
            {
                case QueryOp.Eq:
                    return BsonValueEquals(bsonValue, value);

                case QueryOp.Ne:
                    return !BsonValueEquals(bsonValue, value);

                case QueryOp.Gt:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble > Convert.ToDouble(value);

                case QueryOp.Ge:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble >= Convert.ToDouble(value);

                case QueryOp.Lt:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble < Convert.ToDouble(value);

                case QueryOp.Le:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble <= Convert.ToDouble(value);

                case QueryOp.Contains:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.Contains(value?.ToString()) ?? false;

                case QueryOp.StartsWith:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.StartsWith(value?.ToString()) ?? false;

                case QueryOp.EndsWith:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.EndsWith(value?.ToString()) ?? false;

                default:
                    return false;
            }
        }

        public static bool BsonValueEquals(BsonValue bsonValue, object value)
        {
            if (value == null) return bsonValue.IsNull;
            if (bsonValue.IsNull) return false;

            if (bsonValue.IsNumber && (value is int || value is long || value is float || value is double))
                return Math.Abs(bsonValue.AsDouble - Convert.ToDouble(value)) < 0.0001;

            if (bsonValue.IsString && value is string s)
                return bsonValue.AsString == s;

            if (bsonValue.IsBoolean && value is bool b)
                return bsonValue.AsBoolean == b;

            return bsonValue.ToString() == value.ToString();
        }
    }
}
