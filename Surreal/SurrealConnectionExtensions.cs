using Microsoft.Extensions.Logging;

using Surreal;

namespace Microsoft.Extensions.DependencyInjection;

public static class SurrealConnectionExtensions
{
    public static IServiceCollection AddSurrealDB(this IServiceCollection services, string url, Action<SurrealOptions> configure)
    {
        var options = new SurrealOptions
        {
            Url = url
        };
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.Add(new ServiceDescriptor(typeof(SurrealConnection), CreateSurrealConnection, options.Lifetime));
        return services;

        static SurrealConnection CreateSurrealConnection(IServiceProvider di)
        {
            return new SurrealConnection(
                options: di.GetRequiredService<SurrealOptions>(),
                logger: di.GetService<ILogger<SurrealConnection>>(),
                rpcLogger: di.GetService<ILogger<JsonRpcClient>>());
        }
    }
}
