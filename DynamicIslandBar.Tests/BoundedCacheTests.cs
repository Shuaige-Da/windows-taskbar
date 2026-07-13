using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class BoundedCacheTests
{
    [Fact]
    public void GetOrAdd_ReusesValueAndStaysWithinCapacity()
    {
        var factoryCalls = 0;
        var cache = new BoundedCache<string, string>(2, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("A", cache.GetOrAdd("a", key =>
        {
            factoryCalls++;
            return key.ToUpperInvariant();
        }));
        Assert.Equal("A", cache.GetOrAdd("A", _ => throw new InvalidOperationException()));
        cache.GetOrAdd("b", key => key);
        cache.GetOrAdd("c", key => key);

        Assert.Equal(1, factoryCalls);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrAdd_RemovesFaultedValueSoItCanBeRetried()
    {
        var cache = new BoundedCache<string, string>(2);

        Assert.Throws<InvalidOperationException>(() =>
            cache.GetOrAdd("a", _ => throw new InvalidOperationException()));

        Assert.Equal("recovered", cache.GetOrAdd("a", _ => "recovered"));
    }
}
