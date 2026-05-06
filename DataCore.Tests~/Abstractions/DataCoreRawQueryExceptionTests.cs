using System;
using AroAro.DataCore;
using Xunit;

namespace DataCore.Tests
{
    public class DataCoreRawQueryExceptionTests
    {
        [Fact]
        public void Message_DoesNotContainParameterValues()
        {
            var sensitiveData = "my-secret-password-12345";
            var ex = new DataCoreRawQueryException(
                "SELECT * FROM users WHERE password = @0",
                new object[] { sensitiveData },
                "query failed",
                new Exception("inner"));

            Assert.DoesNotContain(sensitiveData, ex.Message);
        }

        [Fact]
        public void Message_ContainsParameterCount()
        {
            var ex = new DataCoreRawQueryException(
                "SELECT * FROM t WHERE a = @0 AND b = @1",
                new object[] { "val1", "val2" },
                "query failed",
                new Exception("inner"));

            Assert.Contains("Parameter count: 2", ex.Message);
        }

        [Fact]
        public void Message_ContainsExpression()
        {
            var sql = "SELECT $ FROM tabular_";
            var ex = new DataCoreRawQueryException(
                sql, Array.Empty<object>(), "error", new Exception("inner"));

            Assert.Contains(sql, ex.Message);
        }

        [Fact]
        public void GetFormattedParameters_ReturnsValues()
        {
            var ex = new DataCoreRawQueryException(
                "SQL", new object[] { "abc", 123 }, "err", new Exception("inner"));

            var formatted = ex.GetFormattedParameters();
            Assert.Contains("abc", formatted);
            Assert.Contains("123", formatted);
        }

        [Fact]
        public void GetFormattedParameters_TruncatesLongValues()
        {
            var longValue = new string('x', 100);
            var ex = new DataCoreRawQueryException(
                "SQL", new object[] { longValue }, "err", new Exception("inner"));

            var formatted = ex.GetFormattedParameters();
            Assert.Contains("...", formatted);
            Assert.DoesNotContain(new string('x', 60), formatted);
        }

        [Fact]
        public void GetFormattedParameters_EmptyParams_ReturnsBrackets()
        {
            var ex = new DataCoreRawQueryException(
                "SQL", Array.Empty<object>(), "err", new Exception("inner"));

            Assert.Equal("[]", ex.GetFormattedParameters());
        }

        [Fact]
        public void GetFormattedParameters_NullParams_ReturnsBrackets()
        {
            var ex = new DataCoreRawQueryException(
                "SQL", null, "err", new Exception("inner"));

            Assert.Equal("[]", ex.GetFormattedParameters());
        }

        [Fact]
        public void Properties_ArePreserved()
        {
            var args = new object[] { 1, "two" };
            var inner = new Exception("boom");
            var ex = new DataCoreRawQueryException("SQL", args, "msg", inner);

            Assert.Equal("SQL", ex.Expression);
            Assert.Same(args, ex.Parameters);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void NoLiteDBDependency()
        {
            // Verify no BsonValue in the public API surface
            var ex = new DataCoreRawQueryException("SQL", new object[] { 1 }, "err", new Exception("inner"));
            // Message should only have count, not actual values
            Assert.Contains("Parameter count: 1", ex.Message);
            Assert.DoesNotContain("1", ex.Message.Replace("Parameter count: 1", ""));
        }
    }
}
