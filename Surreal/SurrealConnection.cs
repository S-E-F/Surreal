using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

namespace Surreal;

public class SurrealConnection
{
    private readonly JsonRpcClient _rpc;
    private readonly ILogger<SurrealConnection>? _logger;

    public SurrealConnection(SurrealOptions options, ILogger<SurrealConnection>? logger = null, ILogger<JsonRpcClient>? rpcLogger = null)
    {
        _rpc = new(options.ComputedUri, rpcLogger, options.ConfigureRpc);
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

    public async Task<T?> SelectAsync<T>(string id, CancellationToken ct = default)
    {
        var response = await _rpc.CallAsync("select", ct, id);

        if (!response.RootElement.TryGetProperty("result", out var result))
            return default;

        if (result.ValueKind is not JsonValueKind.Array)
        {
            _logger?.LogInformation("Failed to parse result from response\r\n{Json}", response.ToDisplayString());
            throw new InvalidOperationException("Surreal SELECT result value was not an array");
        }

        if (result.GetArrayLength() is 0)
            return default;

        var options = new JsonSerializerOptions();
        options.Converters.Add(new DateOnlyJsonConverter());

        return JsonSerializer.Deserialize<T>(result[0], options);
    }

    private class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateOnly.FromDateTime(DateTime.ParseExact(reader.GetString(), "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        }

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }

    public async Task<JsonDocument> CreateAsync<T>(string id, T record, CancellationToken ct = default)
    {
        var response = await _rpc.CallAsync("create", ct, id, record);
        return response;
    }
}

public static class SurrealConnectionJsonExtensions
{
    public static string ToDisplayString(this JsonDocument document)
    {
        using var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
        });
        document.WriteTo(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
