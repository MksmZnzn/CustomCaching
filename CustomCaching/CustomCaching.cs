using System;
using System.Collections.Generic;
using System.Threading;

public class CustomCaching<TKey, TValue> : IDisposable
    where TKey : notnull
{
    private readonly Dictionary<TKey, (TValue, DateTime expiry)> _cache = new();
    private readonly object _lock = new();
    private readonly Timer _timer;
    private readonly TimeSpan _ttl;
    private readonly int _cacheSize;
    private bool _disposed = false;

    public CustomCaching(TimeSpan ttl, TimeSpan interval, int cacheSize = 1000)
    {
        _ttl = ttl;
        _timer = new Timer(_ => ClearExpired(), null, interval, interval);
        _cacheSize = cacheSize;
    }

    public void Add(TKey key, TValue value)
    {
        lock (_lock)
        {

            if (_cache.Count >= _cacheSize && !_cache.ContainsKey(key))
            {
                ClearExpired();
                if (_cache.Count >= _cacheSize)
                    throw new InvalidOperationException("Cache is full");
            }
            
            _cache[key] = (value, DateTime.UtcNow + _ttl);    
        }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.expiry > DateTime.UtcNow)
            {
                value = entry.Item1;
                return true;
            }
            
            value = default!;
            
            return false;
        }
    }

    public void ClearExpired()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var expiredKeys = new List<TKey>();

            foreach (var keyValueTuple in _cache)
            {
                if (keyValueTuple.Value.expiry <= now)
                {
                    expiredKeys.Add(keyValueTuple.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _timer.Dispose();
            _cache.Clear();
            
            _disposed = true;
        }
    }
    
    ~CustomCaching() => Dispose();
}