using System;
using AroAro.DataCore;
using Xunit;

namespace DataCore.Tests
{
    public class DataValueTests
    {
        [Fact]
        public void Null_Value_IsNull_ReturnsTrue()
        {
            var dv = new DataValue(null);
            Assert.True(dv.IsNull);
        }

        [Fact]
        public void Null_Static_Instance_IsNull()
        {
            Assert.True(DataValue.Null.IsNull);
        }

        [Fact]
        public void Default_IsNull()
        {
            var dv = default(DataValue);
            Assert.True(dv.IsNull);
        }

        [Fact]
        public void AsInt32_WithInt_ReturnsValue()
        {
            var dv = new DataValue(42);
            Assert.Equal(42, dv.AsInt32);
        }

        [Fact]
        public void AsInt32_WithDouble_Converts()
        {
            var dv = new DataValue(3.14);
            Assert.Equal(3, dv.AsInt32);
        }

        [Fact]
        public void AsInt32_Null_ReturnsZero()
        {
            var dv = DataValue.Null;
            Assert.Equal(0, dv.AsInt32);
        }

        [Fact]
        public void AsInt64_WithLong_ReturnsValue()
        {
            var dv = new DataValue(123456789L);
            Assert.Equal(123456789L, dv.AsInt64);
        }

        [Fact]
        public void AsDouble_WithDouble_ReturnsValue()
        {
            var dv = new DataValue(2.718);
            Assert.Equal(2.718, dv.AsDouble);
        }

        [Fact]
        public void AsDouble_Null_ReturnsZero()
        {
            var dv = DataValue.Null;
            Assert.Equal(0.0, dv.AsDouble);
        }

        [Fact]
        public void AsString_WithValue_ReturnsToString()
        {
            var dv = new DataValue("hello");
            Assert.Equal("hello", dv.AsString);
        }

        [Fact]
        public void AsString_Null_ReturnsEmpty()
        {
            var dv = DataValue.Null;
            Assert.Equal(string.Empty, dv.AsString);
        }

        [Fact]
        public void AsBoolean_WithTrue_ReturnsTrue()
        {
            var dv = new DataValue(true);
            Assert.True(dv.AsBoolean);
        }

        [Fact]
        public void AsBoolean_WithFalse_ReturnsFalse()
        {
            var dv = new DataValue(false);
            Assert.False(dv.AsBoolean);
        }

        [Fact]
        public void AsBoolean_WithNonBool_ReturnsFalse()
        {
            var dv = new DataValue(1);
            Assert.False(dv.AsBoolean);
        }

        [Fact]
        public void AsBoolean_Null_ReturnsFalse()
        {
            var dv = DataValue.Null;
            Assert.False(dv.AsBoolean);
        }

        [Fact]
        public void AsString_WithDateTime_ReturnsString()
        {
            var dt = new DateTime(2025, 1, 1);
            var dv = new DataValue(dt);
            Assert.Contains("2025", dv.AsString);
        }
    }
}
