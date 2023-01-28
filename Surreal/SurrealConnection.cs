using System.Net.WebSockets;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Surreal;

public class SurrealConnection
{
    private readonly JsonRpcClient _rpc;
    private readonly ILogger<SurrealConnection>? _logger;

    public SurrealConnection(string url, ILogger<SurrealConnection>? logger = null, ILogger<JsonRpcClient>? rpcLogger = null)
    {
        _rpc = new(new Uri($"ws://{url}/rpc"), rpcLogger);
        _logger = logger;
    }

    public async Task OpenAsync()
    {
        await _rpc.OpenAsync();
    }

    public async Task<bool> SignInAsync(string user, string pass, CancellationToken ct = default)
    {
        var response = await _rpc.CallAsync("signin", ct, new
        {
            user,
            pass,
            method = "basic",
        });

        if (response.RootElement.TryGetProperty("result", out var result) && result.ValueKind is JsonValueKind.String)
            return true;

        return false;
    }

    public async Task<JsonDocument> UseAsync(string @namespace, string database, CancellationToken ct = default)
    {
        var response = await _rpc.CallAsync("use", ct, @namespace, database);
        return response;
    }

    public async Task<JsonDocument> QueryAsync(string query, object? parameters = null, CancellationToken ct = default)
    {
        var response = await _rpc.CallAsync("query", ct, query, parameters);
        return response;
    }
}

