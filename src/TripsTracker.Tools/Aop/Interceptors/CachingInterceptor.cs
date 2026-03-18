using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using TripsTracker.Tools.Aop.Attributes;

namespace TripsTracker.Tools.Aop.Interceptors;

/// <summary>
/// AOP interceptor that caches method return values using IMemoryCache.
/// Activated by <see cref="CacheAttribute"/> on a method.
/// Cache key is built from the method name and serialized arguments.
/// </summary>
public class CachingInterceptor : IInterceptor
{
    private readonly IMemoryCache _cache;

    public CachingInterceptor(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Intercept(IInvocation invocation)
    {
        var attribute = invocation.Method.GetCustomAttributes(typeof(CacheAttribute), true)
            .FirstOrDefault() as CacheAttribute;

        if (attribute is null)
        {
            invocation.Proceed();
            return;
        }

        var cacheKey = BuildCacheKey(invocation, attribute);

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            invocation.ReturnValue = cached;
            return;
        }

        invocation.Proceed();

        if (invocation.ReturnValue is not null)
        {
            _cache.Set(cacheKey, invocation.ReturnValue,
                TimeSpan.FromSeconds(attribute.DurationSeconds));
        }
    }

    private static string BuildCacheKey(IInvocation invocation, CacheAttribute attribute)
    {
        var prefix = attribute.KeyPrefix
            ?? $"{invocation.TargetType.Name}.{invocation.Method.Name}";

        if (invocation.Arguments.Length == 0)
            return prefix;

        try
        {
            var args = JsonSerializer.Serialize(invocation.Arguments);
            return $"{prefix}:{args}";
        }
        catch
        {
            return prefix;
        }
    }
}
