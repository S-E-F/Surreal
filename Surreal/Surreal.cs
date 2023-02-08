using Surreal;

namespace Microsoft.Extensions.DependencyInjection;

public static class Surreal
{
    public static IServiceCollection AddSurrealDB(this IServiceCollection services, string url, Action<SurrealOptions> configure)
    {
        var options = new SurrealOptions
        {
            Url = url
        };
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.Add<SurrealConnection>(options.Lifetime);

        return services;
    }
}
