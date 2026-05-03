using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

public class CaliforniaHousingDataset
{
    private const int NUM_ROWS = 50;

    [Serializable]
    public class CaliforniaHousingData
    {
        public float Longitude;
        public float Latitude;
        public float HousingMedianAge;
        public float TotalRooms;
        public float TotalBedrooms;
        public float Population;
        public float Households;
        public float MedianIncome;
        public float MedianHouseValue;
    }

    /// <summary>
    /// Loads the California housing dataset. Attempts to load from Resources first.
    /// If not found, returns a representative fallback dataset with 50 rows.
    /// </summary>
    /// <returns>An enumerable of CaliforniaHousingData entries.</returns>
    public static IEnumerable<CaliforniaHousingData> Load()
    {
        var dataset = LoadFromResources();
        return dataset ?? GetFallbackData();
    }

    private static IEnumerable<CaliforniaHousingData> LoadFromResources()
    {
        TextAsset csvFile = Resources.Load<TextAsset>("AroAro/DataCore/california_housing_test");
        if (csvFile == null) return null;

        var lines = csvFile.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var data = new List<CaliforniaHousingData>();

        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length < 9) continue;

            data.Add(new CaliforniaHousingData
            {
                Longitude = ParseFloat(values[0]),
                Latitude = ParseFloat(values[1]),
                HousingMedianAge = ParseFloat(values[2]),
                TotalRooms = ParseFloat(values[3]),
                TotalBedrooms = ParseFloat(values[4]),
                Population = ParseFloat(values[5]),
                Households = ParseFloat(values[6]),
                MedianIncome = ParseFloat(values[7]),
                MedianHouseValue = ParseFloat(values[8])
            });
        }

        return data;
    }

    /// <summary>
    /// Get fallback data (50 rows) for environments where the CSV is not bundled.
    /// Designed to be statistically representative for demos and testing.
    /// </summary>
    private static IEnumerable<CaliforniaHousingData> GetFallbackData()
    {
        Debug.Log("Using expanded fallback dataset (50 rows). " +
                  "For full analysis, add california_housing_test.csv to Resources/AroAro/DataCore/");

        var fallbackData = new List<CaliforniaHousingData>
        {
            new CaliforniaHousingData { Longitude = -122.23f, Latitude = 37.88f, HousingMedianAge = 41f, TotalRooms = 880f, TotalBedrooms = 129f, Population = 322f, Households = 126f, MedianIncome = 8.3252f, MedianHouseValue = 452600f },
            new CaliforniaHousingData { Longitude = -122.22f, Latitude = 37.86f, HousingMedianAge = 21f, TotalRooms = 7099f, TotalBedrooms = 1106f, Population = 2401f, Households = 1138f, MedianIncome = 8.3014f, MedianHouseValue = 358500f },
            new CaliforniaHousingData { Longitude = -122.24f, Latitude = 37.85f, HousingMedianAge = 52f, TotalRooms = 1467f, TotalBedrooms = 190f, Population = 496f, Households = 177f, MedianIncome = 7.2574f, MedianHouseValue = 352100f },
            new CaliforniaHousingData { Longitude = -122.25f, Latitude = 37.85f, HousingMedianAge = 52f, TotalRooms = 1274f, TotalBedrooms = 235f, Population = 558f, Households = 219f, MedianIncome = 5.6431f, MedianHouseValue = 341300f },
            new CaliforniaHousingData { Longitude = -122.25f, Latitude = 37.85f, HousingMedianAge = 52f, TotalRooms = 1627f, TotalBedrooms = 280f, Population = 565f, Households = 259f, MedianIncome = 3.8462f, MedianHouseValue = 342200f },
            new CaliforniaHousingData { Longitude = -122.25f, Latitude = 37.85f, HousingMedianAge = 52f, TotalRooms = 919f, TotalBedrooms = 213f, Population = 413f, Households = 193f, MedianIncome = 4.0368f, MedianHouseValue = 269700f },
            new CaliforniaHousingData { Longitude = -122.25f, Latitude = 37.84f, HousingMedianAge = 52f, TotalRooms = 2552f, TotalBedrooms = 489f, Population = 1094f, Households = 490f, MedianIncome = 3.6591f, MedianHouseValue = 299200f },
            new CaliforniaHousingData { Longitude = -122.25f, Latitude = 37.84f, HousingMedianAge = 52f, TotalRooms = 1007f, TotalBedrooms = 195f, Population = 346f, Households = 159f, MedianIncome = 3.12f, MedianHouseValue = 210700f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.84f, HousingMedianAge = 42f, TotalRooms = 1568f, TotalBedrooms = 236f, Population = 719f, Households = 240f, MedianIncome = 2.0804f, MedianHouseValue = 233100f },
            new CaliforniaHousingData { Longitude = -122.25f, Latitude = 37.84f, HousingMedianAge = 42f, TotalRooms = 1601f, TotalBedrooms = 250f, Population = 650f, Households = 252f, MedianIncome = 3.6912f, MedianHouseValue = 254700f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 2899f, TotalBedrooms = 514f, Population = 1465f, Households = 526f, MedianIncome = 3.2031f, MedianHouseValue = 247700f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1857f, TotalBedrooms = 306f, Population = 851f, Households = 312f, MedianIncome = 3.2705f, MedianHouseValue = 242600f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 2205f, TotalBedrooms = 329f, Population = 986f, Households = 334f, MedianIncome = 3.075f, MedianHouseValue = 225500f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1702f, TotalBedrooms = 262f, Population = 832f, Households = 284f, MedianIncome = 2.6768f, MedianHouseValue = 210800f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1428f, TotalBedrooms = 204f, Population = 724f, Households = 215f, MedianIncome = 1.9149f, MedianHouseValue = 201400f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1500f, TotalBedrooms = 248f, Population = 778f, Households = 234f, MedianIncome = 2.3038f, MedianHouseValue = 195900f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1405f, TotalBedrooms = 232f, Population = 704f, Households = 225f, MedianIncome = 2.1299f, MedianHouseValue = 185300f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1393f, TotalBedrooms = 228f, Population = 688f, Households = 222f, MedianIncome = 2.2005f, MedianHouseValue = 183300f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1390f, TotalBedrooms = 228f, Population = 680f, Households = 219f, MedianIncome = 2.2128f, MedianHouseValue = 178900f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1378f, TotalBedrooms = 226f, Population = 672f, Households = 217f, MedianIncome = 2.3287f, MedianHouseValue = 173000f },
            new CaliforniaHousingData { Longitude = -122.26f, Latitude = 37.85f, HousingMedianAge = 42f, TotalRooms = 1370f, TotalBedrooms = 225f, Population = 664f,