using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using TripsTracker.Tools.Aop.Interceptors;

namespace TripsTracker.Tools.Registration;

/// <summary>
/// Registers Castle DynamicProxy and all AOP interceptors into the DI container.
/// </summary>
public static class AopRegistrationExtensions
{
    /// <summary>
    /// Registers the ProxyGenerator and all application interceptors.
    /// Call this from the root project before AddApplicationServices.
    /// </summary>
    public static IServiceCollection AddAopInfrastructure(this IServiceCollection services)
    {
        // Castle DynamicProxy generator — singleton, thread-safe
        services.AddSingleton<IProxyGenerator, ProxyGenerator>();

        // Interceptors — scoped to match service lifetime
        services.AddScoped<LoggingInterceptor>();
        services.AddScoped<ExceptionInterceptor>();
        services.AddScoped<TransactionInterceptor>();
        services.AddScoped<ValidationInterceptor>();
        services.AddScoped<PerformanceInterceptor>();
        services.AddScoped<CachingInterceptor>();

        // Memory cache for CachingInterceptor
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Wraps a registered service with a Castle DynamicProxy interface proxy,
    /// applying the specified interceptors in order.
    /// Call after the service has been registered.
    /// </summary>
    public static IServiceCollection DecorateWithProxy<TInterface>(
        this IServiceCollection services,
        params Type[] interceptorTypes)
        where TInterface : class
    {
        services.Decorate<TInterface>((inner, provider) =>
        {
            var generator = provider.GetRequiredService<IProxyGenerator>();
            var interceptors = interceptorTypes
                .Select(t => (IInterceptor)provider.GetRequiredService(t))
                .ToArray();

            return generator.CreateInterfaceProxyWithTarget<TInterface>(inner, interceptors);
        });

        return services;
    }
}
