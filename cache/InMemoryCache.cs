using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class InMemoryCache : ICache
{
    private readonly MemoryCache cache;

    public InMemoryCache()
    {
        var options = new MemoryCacheOptions { SizeLimit = Config.CACHE_SIZE_IN_MB * 1024 * 1024 };
        this.cache = new MemoryCache(options);
    }

    public async Task<CacheEntry> GetOrSetAsync(
        string key,
        Func<CacheEntry, Task>? onGet,
        Func<Task<CacheEntry>> onSet)
    {
        // get or create
        var created = false;
        var value = await cache.GetOrCreateAsync(key, async (entry) =>
            {
                created = true;
                var result = await onSet();
                entry.AbsoluteExpiration = result.Expiry;
                entry.SetSize(result.Length);
                return result;
            })
            ?? throw new Exception("value should not be null.");

        // if not created, call onGet()
        if (!created && onGet is not null)
        {
            await onGet(value);
        }
        return value;
    }
}