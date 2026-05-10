using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AroAro.DataCore;
using AroAro.DataCore.Tabular;
using Xunit;

namespace DataCore.Tests.Tabular
{
    /// <summary>
    /// TabularData (in-memory) row operation tests.
    /// Covers correctness for AddRow/UpdateRow/DeleteRow and bulk performance.
    /// </summary>
    public class TabularDataTests
    {
        #region AddRow Correctness

        [Fact]
        public void AddRow_FirstRow_InitializesColumns()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 1.0, ["name"] = "a" });

            Assert.Equal(1, t.RowCount);
            Assert.Equal(2, t.ColumnCount);
            Assert.True(t.HasColumn("x"));
            Assert.True(t.HasColumn("name"));
        }

        [Fact]
        public void AddRow_MultipleRows_IncrementsCount()
        {
            var t = CreateNumericTable(100);
            Assert.Equal(100, t.RowCount);
        }

        [Fact]
        public void AddRow_Values_AreRetrievable()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 42.0, ["y"] = 99.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 7.0, ["y"] = 8.0 });

            var row0 = t.GetRow(0);
            var row1 = t.GetRow(1);

            Assert.Equal(42.0, row0["x"]);
            Assert.Equal(99.0, row0["y"]);
            Assert.Equal(7.0, row1["x"]);
            Assert.Equal(8.0, row1["y"]);
        }

        [Fact]
        public void AddRow_NullValues_ThrowsArgumentNullException()
        {
            var t = new TabularData("test");
            Assert.Throws<ArgumentNullException>(() => t.AddRow(null));
        }

        [Fact]
        public void AddRow_MissingColumn_DefaultsToZero()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 1.0, ["y"] = 2.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 3.0 }); // y missing

            var row = t.GetRow(1);
            Assert.Equal(3.0, row["x"]);
            Assert.Equal(0.0, row["y"]);
        }

        [Fact]
        public void AddRow_StringColumn_DefaultsToEmpty()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["name"] = "alice" });
            t.AddRow(new Dictionary<string, object> { }); // name missing

            var row = t.GetRow(1);
            Assert.Equal("", row["name"]);
        }

        #endregion

        #region UpdateRow Correctness

        [Fact]
        public void UpdateRow_ModifiesValuesCorrectly()
        {
            var t = CreateNumericTable(5);
            t.UpdateRow(2, new Dictionary<string, object> { ["col0"] = 999.0 });

            var row = t.GetRow(2);
            Assert.Equal(999.0, row["col0"]);
        }

        [Fact]
        public void UpdateRow_PartialUpdate_OnlyChangesSpecifiedColumns()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 1.0, ["y"] = 2.0 });
            t.UpdateRow(0, new Dictionary<string, object> { ["x"] = 100.0 });

            var row = t.GetRow(0);
            Assert.Equal(100.0, row["x"]);
            Assert.Equal(2.0, row["y"]); // unchanged
        }

        [Fact]
        public void UpdateRow_OutOfRange_ReturnsFalse()
        {
            var t = CreateNumericTable(3);
            Assert.False(t.UpdateRow(-1, new Dictionary<string, object> { ["col0"] = 0 }));
            Assert.False(t.UpdateRow(3, new Dictionary<string, object> { ["col0"] = 0 }));
        }

        [Fact]
        public void UpdateRow_UnknownColumn_IsSkipped()
        {
            var t = CreateNumericTable(3);
            var result = t.UpdateRow(0, new Dictionary<string, object> { ["nonexistent"] = 42.0 });
            Assert.True(result); // returns true, just skips unknown columns
        }

        #endregion

        #region DeleteRow Correctness

        [Fact]
        public void DeleteRow_DecreasesRowCount()
        {
            var t = CreateNumericTable(5);
            t.DeleteRow(2);
            Assert.Equal(4, t.RowCount);
        }

        [Fact]
        public void DeleteRow_FirstRow_AllSubsequentRowsShift()
        {
            var t = new TabularData("test");
            for (int i = 0; i < 5; i++)
                t.AddRow(new Dictionary<string, object> { ["x"] = (double)i });

            t.DeleteRow(0);

            Assert.Equal(4, t.RowCount);
            Assert.Equal(1.0, t.GetRow(0)["x"]);
            Assert.Equal(2.0, t.GetRow(1)["x"]);
            Assert.Equal(3.0, t.GetRow(2)["x"]);
            Assert.Equal(4.0, t.GetRow(3)["x"]);
        }

        [Fact]
        public void DeleteRow_LastRow_PreviousRowsUnaffected()
        {
            var t = new TabularData("test");
            for (int i = 0; i < 5; i++)
                t.AddRow(new Dictionary<string, object> { ["x"] = (double)i });

            t.DeleteRow(4);

            Assert.Equal(4, t.RowCount);
            Assert.Equal(0.0, t.GetRow(0)["x"]);
            Assert.Equal(3.0, t.GetRow(3)["x"]);
        }

        [Fact]
        public void DeleteRow_MiddleRow_ReindexesCorrectly()
        {
            var t = new TabularData("test");
            for (int i = 0; i < 5; i++)
                t.AddRow(new Dictionary<string, object> { ["x"] = (double)i, ["y"] = (double)(i * 10) });

            t.DeleteRow(2); // remove value=2

            Assert.Equal(4, t.RowCount);
            Assert.Equal(0.0, t.GetRow(0)["x"]);
            Assert.Equal(1.0, t.GetRow(1)["x"]);
            Assert.Equal(3.0, t.GetRow(2)["x"]);
            Assert.Equal(4.0, t.GetRow(3)["x"]);
            // Verify y column also shifted
            Assert.Equal(0.0, t.GetRow(0)["y"]);
            Assert.Equal(10.0, t.GetRow(1)["y"]);
            Assert.Equal(30.0, t.GetRow(2)["y"]);
            Assert.Equal(40.0, t.GetRow(3)["y"]);
        }

        [Fact]
        public void DeleteRow_OutOfRange_ReturnsFalse()
        {
            var t = CreateNumericTable(3);
            Assert.False(t.DeleteRow(-1));
            Assert.False(t.DeleteRow(3));
        }

        [Fact]
        public void DeleteRow_AllRows_LeavesEmpty()
        {
            var t = CreateNumericTable(3);
            t.DeleteRow(0);
            t.DeleteRow(0);
            t.DeleteRow(0);
            Assert.Equal(0, t.RowCount);
        }

        #endregion

        #region Bulk Operations & Performance

        [Fact]
        public void AddRow_Bulk_10kRows_CompletesWithinReasonableTime()
        {
            var t = new TabularData("perf");
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < 10_000; i++)
            {
                t.AddRow(new Dictionary<string, object>
                {
                    ["a"] = (double)i,
                    ["b"] = (double)(i * 2),
                    ["c"] = (double)(i * 3)
                });
            }

            sw.Stop();
            Assert.Equal(10_000, t.RowCount);
            Assert.True(sw.ElapsedMilliseconds < 5000,
                $"AddRow 10k took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
        }

        [Fact]
        public void AddRow_Bulk_ValuesCorrectAfterBulkInsert()
        {
            var t = new TabularData("test");
            int n = 1000;
            for (int i = 0; i < n; i++)
                t.AddRow(new Dictionary<string, object> { ["val"] = (double)i });

            Assert.Equal(n, t.RowCount);
            Assert.Equal(0.0, t.GetRow(0)["val"]);
            Assert.Equal(999.0, t.GetRow(999)["val"]);
            Assert.Equal(500.0, t.GetRow(500)["val"]);
        }

        [Fact]
        public void UpdateRow_Bulk_AllRowsCorrect()
        {
            var t = CreateNumericTable(1000);

            for (int i = 0; i < 1000; i++)
                t.UpdateRow(i, new Dictionary<string, object> { ["col0"] = (double)(i * 10) });

            Assert.Equal(0.0, t.GetRow(0)["col0"]);
            Assert.Equal(9990.0, t.GetRow(999)["col0"]);
        }

        [Fact]
        public void DeleteRow_Bulk_MaintainsCorrectness()
        {
            var t = new TabularData("test");
            for (int i = 0; i < 100; i++)
                t.AddRow(new Dictionary<string, object> { ["x"] = (double)i });

            // Delete every other row from the end
            for (int i = 99; i >= 0; i -= 2)
                t.DeleteRow(i);

            Assert.Equal(50, t.RowCount);
            // Remaining should be 0, 2, 4, 6, ...
            for (int i = 0; i < 50; i++)
                Assert.Equal((double)(i * 2), t.GetRow(i)["x"]);
        }

        [Fact]
        public void MixedOperations_MaintainsConsistency()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 1.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 2.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 3.0 });

            t.UpdateRow(1, new Dictionary<string, object> { ["x"] = 20.0 });
            t.DeleteRow(0); // remove 1.0, shifts: [20.0, 3.0]
            t.AddRow(new Dictionary<string, object> { ["x"] = 4.0 }); // [20.0, 3.0, 4.0]

            Assert.Equal(3, t.RowCount);
            Assert.Equal(20.0, t.GetRow(0)["x"]);
            Assert.Equal(3.0, t.GetRow(1)["x"]);
            Assert.Equal(4.0, t.GetRow(2)["x"]);
        }

        #endregion

        #region GetNumericColumn

        [Fact]
        public void GetNumericColumn_ReturnsNDArray()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 1.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 2.0 });

            var col = t.GetNumericColumn("x");
            Assert.Equal(1.0, col.GetDouble(0));
            Assert.Equal(2.0, col.GetDouble(1));
        }

        [Fact]
        public void GetNumericColumn_AfterUpdate_ReflectsChanges()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 1.0 });
            t.UpdateRow(0, new Dictionary<string, object> { ["x"] = 99.0 });

            var col = t.GetNumericColumn("x");
            Assert.Equal(99.0, col.GetDouble(0));
        }

        [Fact]
        public void GetNumericColumn_AfterDelete_ReflectsShift()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 10.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 20.0 });
            t.DeleteRow(0);

            var col = t.GetNumericColumn("x");
            Assert.Equal(20.0, col.GetDouble(0));
        }

        #endregion

        #region Issue #45 — GetNumericColumn returns defensive copy

        [Fact]
        public void GetNumericColumn_ReturnsDefensiveCopy_ModifyingResultDoesNotAffectInternal()
        {
            var t = new TabularData("test");
            t.AddNumericColumn("x", new double[] { 1.0, 2.0, 3.0 });

            // Get the numeric column
            var col = t.GetNumericColumn("x");

            // Modify the returned NDArray
            col[0] = 999.0;
            col[1] = 888.0;
            col[2] = 777.0;

            // Verify internal data is unaffected
            var original = t.GetNumericColumn("x");
            Assert.Equal(1.0, original.GetDouble(0));
            Assert.Equal(2.0, original.GetDouble(1));
            Assert.Equal(3.0, original.GetDouble(2));

            // Also verify via GetRow
            Assert.Equal(1.0, t.GetRow(0)["x"]);
            Assert.Equal(2.0, t.GetRow(1)["x"]);
            Assert.Equal(3.0, t.GetRow(2)["x"]);
        }

        [Fact]
        public void GetNumericColumn_MultipleCallsReturnIndependentCopies()
        {
            var t = new TabularData("test");
            t.AddNumericColumn("x", new double[] { 10.0, 20.0 });

            var col1 = t.GetNumericColumn("x");
            var col2 = t.GetNumericColumn("x");

            // Modify col1
            col1[0] = 999.0;

            // col2 should be unaffected
            Assert.Equal(10.0, col2.GetDouble(0));
        }

        #endregion

        #region Where

        [Fact]
        public void Where_NumericEq_FiltersCorrectly()
        {
            var t = CreateNumericTable(10);
            var indices = t.Where("col0", QueryOp.Eq, 5.0);
            Assert.Single(indices);
            Assert.Equal(5, indices[0]);
        }

        [Fact]
        public void Where_NumericGt_FiltersCorrectly()
        {
            var t = CreateNumericTable(10);
            var indices = t.Where("col0", QueryOp.Gt, 7.0);
            Assert.Equal(2, indices.Length); // 8, 9
        }

        [Fact]
        public void Where_StringContains_FiltersCorrectly()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["name"] = "alice" });
            t.AddRow(new Dictionary<string, object> { ["name"] = "bob" });
            t.AddRow(new Dictionary<string, object> { ["name"] = "charlie" });

            var indices = t.Where("name", QueryOp.Contains, "li");
            Assert.Equal(2, indices.Length); // alice, charlie
        }

        #endregion

        #region Sum/Average/Min/Max

        [Fact]
        public void Sum_CalculatesCorrectly()
        {
            var t = new TabularData("test");
            for (int i = 1; i <= 100; i++)
                t.AddRow(new Dictionary<string, object> { ["x"] = (double)i });

            Assert.Equal(5050.0, t.Sum("x"));
        }

        [Fact]
        public void Average_CalculatesCorrectly()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 10.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 20.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 30.0 });

            Assert.Equal(20.0, t.Average("x"));
        }

        [Fact]
        public void Min_Max_ReturnCorrectValues()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 5.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 1.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 9.0 });

            Assert.Equal(1.0, t.Min("x"));
            Assert.Equal(9.0, t.Max("x"));
        }

        #endregion

        #region Clear

        [Fact]
        public void Clear_ResetsRowCount()
        {
            var t = CreateNumericTable(10);
            t.Clear();
            Assert.Equal(0, t.RowCount);
        }

        [Fact]
        public void Clear_PreservesColumns()
        {
            var t = CreateNumericTable(10);
            t.Clear();
            Assert.True(t.HasColumn("col0"));
            Assert.Equal(ColumnType.Numeric, t.GetColumnType("col0"));
        }

        #endregion

        #region ImportFromCsv / ExportToCsv

        [Fact]
        public void ImportFromCsv_BasicImport_Works()
        {
            var t = new TabularData("test");
            t.ImportFromCsv("a,b\n1,hello\n2,world");

            Assert.Equal(2, t.RowCount);
            Assert.Equal(2, t.ColumnCount);
            Assert.Equal(1.0, t.GetRow(0)["a"]);
            Assert.Equal("hello", t.GetRow(0)["b"]);
        }

        [Fact]
        public void ExportToCsv_RoundTrip_DataPreserved()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["x"] = 1.0, ["y"] = 2.0 });
            t.AddRow(new Dictionary<string, object> { ["x"] = 3.0, ["y"] = 4.0 });

            var csv = t.ExportToCsv();
            var t2 = new TabularData("roundtrip");
            t2.ImportFromCsv(csv);

            Assert.Equal(t.RowCount, t2.RowCount);
            Assert.Equal(1.0, t2.GetRow(0)["x"]);
            Assert.Equal(4.0, t2.GetRow(1)["y"]);
        }

        #endregion

        #region Issue #53 — AddRow numeric type detection for all IConvertible types

        [Fact]
        public void AddRow_DecimalValue_ColumnDetectedAsNumeric()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["price"] = 19.99m });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("price"));
            Assert.Equal(1, t.RowCount);
        }

        [Fact]
        public void AddRow_ShortValue_ColumnDetectedAsNumeric()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["val"] = (short)42 });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("val"));
        }

        [Fact]
        public void AddRow_ByteValue_ColumnDetectedAsNumeric()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["val"] = (byte)255 });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("val"));
        }

        [Fact]
        public void AddRow_UIntValue_ColumnDetectedAsNumeric()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["val"] = (uint)100000 });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("val"));
        }

        [Fact]
        public void AddRow_ULongValue_ColumnDetectedAsNumeric()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["val"] = (ulong)9999999999 });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("val"));
        }

        [Fact]
        public void AddRow_SByteValue_ColumnDetectedAsNumeric()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["val"] = (sbyte)-10 });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("val"));
        }

        [Fact]
        public void AddRow_UShortValue_ColumnDetectedAsNumeric()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["val"] = (ushort)65535 });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("val"));
        }

        [Fact]
        public void AddRow_DecimalValues_MultipleRows_RetrievableAsDouble()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["price"] = 9.99m });
            t.AddRow(new Dictionary<string, object> { ["price"] = 19.99m });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("price"));
            Assert.Equal(2, t.RowCount);
            // Values should be convertible via GetNumericColumn
            var col = t.GetNumericColumn("price");
            Assert.Equal(9.99, col.GetDouble(0), 2);
            Assert.Equal(19.99, col.GetDouble(1), 2);
        }

        [Fact]
        public void AddRow_MixedIConvertibleTypes_AllDetectedAsNumeric()
        {
            var t = new TabularData("test");
            // First row initializes column types
            t.AddRow(new Dictionary<string, object> { ["a"] = (decimal)1.1m, ["b"] = (short)2, ["c"] = (byte)3 });
            // Subsequent rows with different IConvertible types
            t.AddRow(new Dictionary<string, object> { ["a"] = (uint)4, ["b"] = (ulong)5, ["c"] = (sbyte)6 });

            Assert.Equal(ColumnType.Numeric, t.GetColumnType("a"));
            Assert.Equal(ColumnType.Numeric, t.GetColumnType("b"));
            Assert.Equal(ColumnType.Numeric, t.GetColumnType("c"));
            Assert.Equal(2, t.RowCount);
        }

        #endregion

        #region Issue #51 — Type-aware ordering in ToDictionaries()

        [Fact]
        public void OrderBy_NumericColumn_SortsCorrectly()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["val"] = 30.0, ["name"] = "c" });
            t.AddRow(new Dictionary<string, object> { ["val"] = 10.0, ["name"] = "a" });
            t.AddRow(new Dictionary<string, object> { ["val"] = 20.0, ["name"] = "b" });

            var result = t.Query().OrderBy("val").ToDictionaries();

            Assert.Equal(3, result.Count);
            Assert.Equal(10.0, result[0]["val"]);
            Assert.Equal(20.0, result[1]["val"]);
            Assert.Equal(30.0, result[2]["val"]);
        }

        [Fact]
        public void OrderBy_StringColumn_SortsCorrectly()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["name"] = "charlie", ["val"] = 1.0 });
            t.AddRow(new Dictionary<string, object> { ["name"] = "alice", ["val"] = 2.0 });
            t.AddRow(new Dictionary<string, object> { ["name"] = "bob", ["val"] = 3.0 });

            var result = t.Query().OrderBy("name").ToDictionaries();

            Assert.Equal(3, result.Count);
            Assert.Equal("alice", result[0]["name"]);
            Assert.Equal("bob", result[1]["name"]);
            Assert.Equal("charlie", result[2]["name"]);
        }

        [Fact]
        public void OrderByDescending_NumericColumn_SortsDescending()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["val"] = 5.0 });
            t.AddRow(new Dictionary<string, object> { ["val"] = 1.0 });
            t.AddRow(new Dictionary<string, object> { ["val"] = 9.0 });
            t.AddRow(new Dictionary<string, object> { ["val"] = 3.0 });

            var result = t.Query().OrderByDescending("val").ToDictionaries();

            Assert.Equal(4, result.Count);
            Assert.Equal(9.0, result[0]["val"]);
            Assert.Equal(5.0, result[1]["val"]);
            Assert.Equal(3.0, result[2]["val"]);
            Assert.Equal(1.0, result[3]["val"]);
        }

        #endregion

        #region Issue #47 — GetStringColumn returns defensive copy

        [Fact]
        public void GetStringColumn_ReturnsDefensiveCopy_ModifyingResultDoesNotAffectInternal()
        {
            var t = new TabularData("test");
            t.AddRow(new Dictionary<string, object> { ["name"] = "alice" });
            t.AddRow(new Dictionary<string, object> { ["name"] = "bob" });
            t.AddRow(new Dictionary<string, object> { ["name"] = "charlie" });

            // Get a copy of the string column
            var copy = t.GetStringColumn("name");

            // Modify the returned array
            copy[0] = "MUTATED";
            copy[1] = "MUTATED";
            copy[2] = "MUTATED";

            // Verify internal data is unaffected
            var original = t.GetStringColumn("name");
            Assert.Equal("alice", original[0]);
            Assert.Equal("bob", original[1]);
            Assert.Equal("charlie", original[2]);

            // Also verify via GetRow
            Assert.Equal("alice", t.GetRow(0)["name"]);
            Assert.Equal("bob", t.GetRow(1)["name"]);
            Assert.Equal("charlie", t.GetRow(2)["name"]);
        }

        #endregion

        #region Helpers

        private static TabularData CreateNumericTable(int rows)
        {
            var t = new TabularData("test");
            for (int i = 0; i < rows; i++)
            {
                t.AddRow(new Dictionary<string, object>
                {
                    ["col0"] = (double)i,
                    ["col1"] = (double)(i * 10)
                });
            }
            return t;
        }

        #endregion
    }
}
