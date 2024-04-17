using System;
using System.Threading.Tasks;

public interface ICache
{
    Task<CacheEntry> GetOrSetAsync(string key, Func<CacheEntry, Task>? onGet, Func<Task<CacheEntry>> onSet);
}