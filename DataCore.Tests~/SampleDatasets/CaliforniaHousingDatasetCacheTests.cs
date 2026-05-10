using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.SampleDatasets;
using Xunit;

namespace DataCore.Tests.SampleDatasets;

/// <summary>
/// Tests for the static caching in CaliforniaHousingDataset.GetSampleData() (issue #74).
/// In the test environment, Resources.Load always returns null, so the fallback
/// dataset is used — but the cache logic (null-check → set → return) is fully exercisable.
/// </summary>
public class CaliforniaHousingDatasetCacheTests
{
    // Clear cache before each test to ensure isolation
    public CaliforniaHousingDatasetCacheTests()
    {
        CaliforniaHousingDataset.ClearCache();
    }

    [Fact]
    [Trait("category", "unity-only")]
    public void GetSampleData_SecondCall_ReturnsSameReference()
    {
        var first = CaliforniaHousingDataset.GetSampleData();
        var second = CaliforniaHousingDataset.GetSampleData();

        Assert.Same(first, second);
    }

    [Fact]
    [Trait("category", "unity-only")]
    public void ClearCache_NextCall_ReturnsNewInstance()
    {
        var first = CaliforniaHousingDataset.GetSampleData();

        CaliforniaHousingDataset.ClearCache();

        var second = CaliforniaHousingDataset.GetSampleData();

        Assert.NotSame(first, second);
    }

    [Fact]
    [Trait("category", "unity-only")]
    public void ClearCache_NextCall_DataIsEquivalent()
    {
        var first = CaliforniaHousingDataset.GetSampleData();

        CaliforniaHousingDataset.ClearCache();

        var second = CaliforniaHousingDataset.GetSampleData();

        // Same keys
        Assert.Equal(first.Keys.OrderBy(k => k), second.Keys.OrderBy(k => k));

        // Same data in every column
        foreach (var key in first.Keys)
        {
            Assert.Equal(first[key], second[key]);
        }
    }

    [Fact]
    [Trait("category", "unity-only")]
    public void GetSampleData_ReturnsExpectedColumns()
    {
        var data = CaliforniaHousingDataset.GetSampleData();

        Assert.Contains("longitude", data.Keys);
        Assert.Contains("latitude", data.Keys);
        Assert.Contains("housing_median_age", data.Keys);
        Assert.Contains("total_rooms", data.Keys);
        Assert.Contains("total_bedrooms", data.Keys);
        Assert.Contains("population", data.Keys);
        Assert.Contains("households", data.Keys);
        Assert.Contains("median_income", data.Keys);
        Assert.Contains("median_house_value", data.Keys);
    }

    [Fact]
    [Trait("category", "unity-only")]
    public void GetSampleData_AllColumnsHaveSameLength()
    {
        var data = CaliforniaHousingDataset.GetSampleData();

        var lengths = data.Values.Select(v => v.Length).Distinct().ToList();
        Assert.Single(lengths);
        Assert.True(lengths[0] > 0, "Expected at least one row of data");
    }

    [Fact]
    [Trait("category", "unity-only")]
    public void ClearCache_MultipleClears_DoesNotThrow()
    {
        CaliforniaHousingDataset.GetSampleData(); // prime cache

        CaliforniaHousingDataset.ClearCache();
        CaliforniaHousingDataset.ClearCache(); // double clear should be safe

        var data = CaliforniaHousingDataset.GetSampleData();
        Assert.NotNull(data);
        Assert.True(data.Count > 0);
    }
}
