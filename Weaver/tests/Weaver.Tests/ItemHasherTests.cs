using Weaver.Domain;
using Weaver.Scraping;
using Xunit;

namespace Weaver.Tests;

public class ItemHasherTests
{
    private static FieldSelector Field(string name, bool isKey, int order = 0) =>
        new() { Name = name, IsKey = isKey, Order = order, Selector = ".x" };

    [Fact]
    public void ComputeItemKey_UsesKeyFieldsInOrder_RegardlessOfDictionaryOrder()
    {
        var fields = new List<FieldSelector> { Field("b", true, 1), Field("a", true, 0) };
        var data1 = new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2" };
        var data2 = new Dictionary<string, string?> { ["b"] = "2", ["a"] = "1" };

        var key1 = ItemHasher.ComputeItemKey(data1, fields, "https://x.com", 0);
        var key2 = ItemHasher.ComputeItemKey(data2, fields, "https://x.com", 0);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void ComputeItemKey_DifferentKeyValues_ProduceDifferentKeys()
    {
        var fields = new List<FieldSelector> { Field("sku", true) };
        var keyA = ItemHasher.ComputeItemKey(new() { ["sku"] = "AAA" }, fields, "https://x.com", 0);
        var keyB = ItemHasher.ComputeItemKey(new() { ["sku"] = "BBB" }, fields, "https://x.com", 0);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void ComputeItemKey_NoKeyFields_FallsBackToUrlAndOrdinal()
    {
        var fields = new List<FieldSelector> { Field("title", false) };
        var data = new Dictionary<string, string?> { ["title"] = "Widget" };

        var keyPage1Item0 = ItemHasher.ComputeItemKey(data, fields, "https://x.com/page1", 0);
        var keyPage1Item1 = ItemHasher.ComputeItemKey(data, fields, "https://x.com/page1", 1);
        var keyPage2Item0 = ItemHasher.ComputeItemKey(data, fields, "https://x.com/page2", 0);

        Assert.NotEqual(keyPage1Item0, keyPage1Item1);
        Assert.NotEqual(keyPage1Item0, keyPage2Item0);
    }

    [Fact]
    public void ComputeItemKey_NoKeyFields_StaysStableWhenDataChanges()
    {
        // This is the whole point of the fallback: the key must NOT depend on content,
        // otherwise a changed item is never recognized as "the same item" across runs.
        var fields = new List<FieldSelector> { Field("price", false) };

        var keyBefore = ItemHasher.ComputeItemKey(new() { ["price"] = "10" }, fields, "https://x.com/page1", 0);
        var keyAfter = ItemHasher.ComputeItemKey(new() { ["price"] = "8" }, fields, "https://x.com/page1", 0);

        Assert.Equal(keyBefore, keyAfter);
    }

    [Fact]
    public void ComputeContentHash_IsOrderIndependent()
    {
        var dataA = new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2" };
        var dataB = new Dictionary<string, string?> { ["b"] = "2", ["a"] = "1" };

        Assert.Equal(ItemHasher.ComputeContentHash(dataA), ItemHasher.ComputeContentHash(dataB));
    }

    [Fact]
    public void ComputeContentHash_ChangesWhenAValueChanges()
    {
        var before = ItemHasher.ComputeContentHash(new() { ["price"] = "10" });
        var after = ItemHasher.ComputeContentHash(new() { ["price"] = "9" });

        Assert.NotEqual(before, after);
    }
}
