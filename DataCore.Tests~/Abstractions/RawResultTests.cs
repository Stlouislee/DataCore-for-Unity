using System;
using AroAro.DataCore;
using Xunit;

namespace DataCore.Tests
{
    public class RawResultTests
    {
        [Fact]
        public void ScalarConstructor_SetsScalarValue()
        {
            var dv = new DataValue(42);
            var result = new RawResult(dv);

            Assert.True(result.HasData == false);
            Assert.Equal(42, result.AsInt32);
            Assert.Equal(42, result.ScalarValue.AsInt32);
        }

        [Fact]
        public void DefaultConstructor_SetsNullScalar()
        {
            var result = new RawResult();

            Assert.Null(result.Data);
            Assert.True(result.ScalarValue.IsNull);
            Assert.False(result.HasData);
        }

        [Fact]
        public void ConvenienceAccessors_DelegateToScalarValue()
        {
            var result = new RawResult(new DataValue(3.14));

            Assert.Equal(3, result.AsInt32);
            Assert.Equal(3.14, result.AsDouble);
        }

        [Fact]
        public void AsString_ReturnsScalarString()
        {
            var result = new RawResult(new DataValue("test"));
            Assert.Equal("test", result.AsString);
        }

        [Fact]
        public void AsBoolean_ReturnsScalarBool()
        {
            var result = new RawResult(new DataValue(true));
            Assert.True(result.AsBoolean);
        }

        [Fact]
        public void ScalarResult_HasNoData()
        {
            var result = new RawResult(new DataValue(1));
            Assert.False(result.HasData);
            Assert.Null(result.Data);
        }

        [Fact]
        public void NoLiteDBDependency_ScalarValueIsDataValue()
        {
            // Verify the type is DataValue, not BsonValue
            var result = new RawResult(new DataValue(100));
            Assert.IsType<DataValue>(result.ScalarValue);
        }
    }
}
