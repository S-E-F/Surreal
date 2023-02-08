namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds a mapped service to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImpl">The actual type.</typeparam>
    /// <param name="services">The extended <see cref="IServiceCollection"/>.</param>
    /// <param name="lifetime">The service lifetime.</param>
    public static void Add<TService, TImpl>(this IServiceCollection services, ServiceLifetime lifetime)
        => services.Add(ServiceDescriptor.Describe(typeof(TService), typeof(TImpl), lifetime));

    /// <summary>
    /// Adds a service to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="services">The extended <see cref="IServiceCollection"/>.</param>
    /// <param name="lifetime">The service lifetime.</param>
    public static void Add<TService>(this IServiceCollection services, ServiceLifetime lifetime)
        => services.Add<TService, TService>(lifetime);
}
