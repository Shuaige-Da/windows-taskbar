using System.Collections.Concurrent;

namespace DynamicIslandBar;

public sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<TKey, CacheEntry> _entries;
    private readonly ConcurrentQueue<CacheEntry> _insertionOrder = new();

    public BoundedCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _entries = new ConcurrentDictionary<TKey, CacheEntry>(comparer ?? EqualityComparer<TKey>.Default);
    }

    public int Count => _entries.Count;

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);

        var candidate = new CacheEntry(
            key,
            new Lazy<TValue>(() => valueFactory(key), LazyThreadSafetyMode.ExecutionAndPublication));
        var entry = _entries.GetOrAdd(key, candidate);
        if (ReferenceEquals(entry, candidate))
        {
            _insertionOrder.Enqueue(entry);
            TrimToCapacity();
        }

        try
        {
            return entry.Value.Value;
        }
        catch
        {
            RemoveEntry(entry);
            throw;
        }
    }

    private void TrimToCapacity()
    {
        while (_entries.Count > _capacity && _insertionOrder.TryDequeue(out var oldest))
        {
            RemoveEntry(oldest);
        }
    }

    private void RemoveEntry(CacheEntry entry)
    {
        ((ICollection<KeyValuePair<TKey, CacheEntry>>)_entries)
            .Remove(new KeyValuePair<TKey, CacheEntry>(entry.Key, entry));
    }

    private sealed record CacheEntry(TKey Key, Lazy<TValue> Value);
}
