using Microsoft.Extensions.DependencyInjection;

namespace JsonRpc;

public static class JsonRpcExtensions
{
    public static void AddJsonRpc(this IServiceCollection services)
    {
        services.AddTransient<JsonRpcClient>();
    }
}
