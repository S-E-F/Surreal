using System.Text.Json;
using System.Text.Json.Serialization;

using JsonRpc;

using Microsoft.Extensions.Logging;

namespace Surreal;

public abstract class SurrealException : Exception
{
    protected SurrealException(string message) : base(message)
    {
    }

    protected SurrealException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class SurrealRecordAlreadyExistsException : SurrealException
{
    public SurrealRecordAlreadyExistsException(string message)
        : base(message) { }

    public SurrealRecordAlreadyExistsException(string message, Exception inner)
        : base(message, inner) { }
}

public sealed class SurrealConnection
{
    private readonly JsonRpcClient _rpc;
    private readonly ILogger<SurrealConnection>? _logger;
    private readonly SurrealOptions _options;

    public SurrealConnection(SurrealOptions options, JsonRpcClient rpc, ILogger<SurrealConnection>? logger = null)
    {
        _rpc = rpc;
        _logger = logger;
        _options = options;
    }

    public async Task OpenAsync()
    {
        _logger?.LogTrace("Attempting to connect to {Url}", _options.ComputedUri);
        await _rpc.OpenAsync(_options.ComputedUri);
    }

    public async Task<bool> SignInAsync(string user, string pass, CancellationToken ct = default)
    {
        var result = await _rpc.CallAsync<string>("signin", ct, new
        {
            user,
            pass,
            method = "basic",
        });

        return result is "";
    }

    public async Task UseAsync(string @namespace, string database, CancellationToken ct = default)
    {
        await _rpc.CallAsync<object?>("use", ct, @namespace, database);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string query, object? parameters = null, CancellationToken ct = default)
    {
        return await _rpc.CallAsync<IEnumerable<T>>("query", ct, query, parameters) ?? Enumerable.Empty<T>();
    }

    public async Task<IEnumerable<T>> SelectAsync<T>(string id, CancellationToken ct = default)
    {
        return await _rpc.CallAsync<IEnumerable<T>>("select", ct, id) ?? Enumerable.Empty<T>();
    }

    public async Task<IEnumerable<T>> CreateAsync<T>(string id, T record, CancellationToken ct = default)
    {
        try
        {
            return await _rpc.CallAsync<IEnumerable<T>>("create", ct, id, record) ?? Enumerable.Empty<T>();
        }
        catch (RpcException error) when (error.Code is -32000)
        {
            throw new SurrealRecordAlreadyExistsException(error.ErrorMessage, error);
        }
    }

    private readonly struct InfoForKvResult
    {
        [JsonPropertyName("ns")]
        public Dictionary<string, string> Namespaces { get; init; }
    }

    public async Task GetNamespacesAsync(CancellationToken ct = default)
    {
        var result = await _rpc.CallAsync<InfoForKvResult>("info", ct);
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
}
