using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

namespace JsonRpc;

public class JsonRpcClient
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ClientWebSocket _socket = new();
    private Task _connection = Task.CompletedTask;
    private readonly ConcurrentDictionary<Guid, CallToken> _responses = new();
    private readonly ConcurrentDictionary<string, HashSet<Func<Task>>> _notificationHandlers = new();
    private readonly ILogger<JsonRpcClient>? _logger;

    private class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateOnly.FromDateTime(DateTime.ParseExact(reader.GetString() ?? throw new InvalidOperationException("Invalid date format"), "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
        }

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToDateTime(TimeOnly.MinValue));
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    static JsonRpcClient()
    {
        _jsonOptions.Converters.Add(new DateOnlyJsonConverter());
    }

    public JsonRpcClient(ILogger<JsonRpcClient>? logger, Action<ClientWebSocketOptions>? configure = null)
    {
        _logger = logger;
        configure?.Invoke(_socket.Options);
    }

    public Uri Url { get; private set; } = default!;

    public void On(string notification, Func<Task> callback)
    {
        if (_notificationHandlers.TryGetValue(notification, out var set))
            set.Add(callback);
        else
            _notificationHandlers.TryAdd(notification, new HashSet<Func<Task>> { callback });
    }

    private readonly struct RpcResponse
    {
        public Guid Id { get; init; }

        public JsonElement? Result { get; init; }

        public JsonElement? Error { get; init; }
    }

    private readonly struct RpcError
    {
        public int Code { get; init; }

        public string Message { get; init; }
    }

    private readonly struct CallToken
    {
        public Guid Id { get; }

        private readonly TaskCompletionSource<RpcResponse> _tcs;

        public CallToken(Guid id)
        {
            Id = id;
            _tcs = new();
        }

        public Task<RpcResponse> Response => _tcs.Task;

        public void SetResponse(RpcResponse result)
        {
            _tcs.SetResult(result);
        }
    }

    public async Task<T?> CallAsync<T>(string method, CancellationToken ct, params object?[] parameters)
    {
        var id = Guid.NewGuid();
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id,
            method,
            @params = parameters,
        }, _jsonOptions);

        if (_logger?.IsEnabled(LogLevel.Information) is true)
            LogCall(method, id, jsonBytes);

        var token = new CallToken(id);
        _responses.TryAdd(id, token);

        await _socket.SendAsync(jsonBytes, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, ct);

        var response = await token.Response;
        _responses.Remove(id, out _);

        if (response.Error is not null)
        {
            var error = JsonSerializer.Deserialize<RpcError>(response.Error.Value.GetRawText(), _jsonOptions);
            throw new InvalidOperationException($"Error {error.Code} for {response.Id}: {error.Message}");
        }

        if (response.Result is null)
            return default;

        return JsonSerializer.Deserialize<T>(response.Result.Value.GetRawText(), _jsonOptions);
    }

    private void LogCall(string method, Guid id, byte[] json)
    {
        if (_logger?.IsEnabled(LogLevel.Trace) is true)
            _logger?.LogTrace("Calling '{Method}' ({Id})\r\n{Json}", method, id, Encoding.UTF8.GetString(json));
        else
            _logger?.LogInformation("Calling '{Method}' ({Id})", method, id);
    }

    private void LogReceive(Stream stream)
    {
        if (_logger?.IsEnabled(LogLevel.Trace) is true)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var doc = JsonSerializer.Deserialize<JsonDocument>(stream, _jsonOptions);
            _logger?.LogTrace("Response:\r\n{Json}", JsonSerializer.Serialize(doc, _jsonOptions));
        }
    }

    public async Task OpenAsync(Uri uri)
    {
        Url = uri;
        var startTime = Stopwatch.GetTimestamp();
        try
        {
            await _socket.ConnectAsync(Url, _cts.Token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to {Url}", Url);
            throw;
        }
        _logger?.LogInformation("Connection established with {Url} in {ElapsedTime}", Url, Stopwatch.GetElapsedTime(startTime));
        _connection = Task.Run(async () =>
        {
            var buffer = new byte[2048];
            var segment = new ArraySegment<byte>(buffer);
            using var stream = new MemoryStream();

            do
            {
                WebSocketReceiveResult result;
                stream.SetLength(0);
                do
                {
                    result = await _socket.ReceiveAsync(segment, _cts.Token);
                    stream.Write(buffer, segment.Offset, result.Count);

                } while (!result.EndOfMessage);

                if (result.MessageType is WebSocketMessageType.Close)
                    break;

                if (_logger?.IsEnabled(LogLevel.Information) is true)
                    LogReceive(stream);

                stream.Seek(0, SeekOrigin.Begin);
                var response = await JsonSerializer.DeserializeAsync<RpcResponse>(stream, _jsonOptions);

                var success = _responses.TryGetValue(response.Id, out var token);

                if (!success)
                    throw new InvalidOperationException($"Failed to retrieve request token for response {response.Id}");

                token.SetResponse(response);

            } while (!_cts.IsCancellationRequested);
        });
    }

    public async Task CloseAsync()
    {
        _cts.Cancel();
        await _connection;
        _connection = Task.CompletedTask;
    }
}