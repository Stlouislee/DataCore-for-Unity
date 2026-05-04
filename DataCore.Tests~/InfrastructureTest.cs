using Xunit;
using LiteDB;

namespace DataCore.Tests;

public class InfrastructureTest
{
    [Fact]
    public void LiteDB_InMemory_Works()
    {
        using var db = new LiteDatabase(":memory:");
        var col = db.GetCollection("test");
        col.Insert(new BsonDocument { ["key"] = "value" });
        var result = col.FindOne(Query.EQ("key", "value"));
        Assert.NotNull(result);
        Assert.Equal("value", result["key"].AsString);
    }
}
