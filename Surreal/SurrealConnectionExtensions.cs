using Microsoft.Extensions.Logging;

using Surreal;

namespace Microsoft.Extensions.DependencyInjection;

public static class SurrealConnectionExtensions
{
    public static IServiceCollection AddSurrealDB(this IServiceCollection services, string url, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        services.Add(new ServiceDescriptor(typeof(SurrealConnection), CreateSurrealConnection, lifetime));
        return services;

        SurrealConnection CreateSurrealConnection(IServiceProvider di)
        {
            return new SurrealConnection(url, di.GetService<ILogger<SurrealConnection>>(), di.GetService<ILogger<JsonRpcClient>>());
        }
    }
}

