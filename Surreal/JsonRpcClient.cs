using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Surreal;

public class JsonRpcClient
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ClientWebSocket _socket = new();
    private Task _connection = Task.CompletedTask;
    private readonly ConcurrentDictionary<Guid, CallToken> _responses = new();
    private readonly ConcurrentDictionary<string, HashSet<Func<Task>>> _notificationHandlers = new();
    private readonly ILogger<JsonRpcClient>? _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public JsonRpcClient(Uri uri, ILogger<JsonRpcClient>? logger, Action<ClientWebSocketOptions>? configure = null)
    {
        Url = uri;
        _logger = logger;
        configure?.Invoke(_socket.Options);
    }

    public Uri Url { get; }

    public void On(string notification, Func<Task> callback)
    {
        if (_notificationHandlers.TryGetValue(notification, out var set))
            set.Add(callback);
        else
            _notificationHandlers.TryAdd(notification, new HashSet<Func<Task>> { callback });
    }

    private readonly struct CallToken
    {
        public Guid Id { get; }

        private readonly TaskCompletionSource<JsonDocument> _tcs;

        public CallToken(Guid id)
        {
            Id = id;
            _tcs = new();
        }

        public Task<JsonDocument> Response => _tcs.Task;

        public void SetResponse(JsonDocument response)
        {
            _tcs.SetResult(response);
        }
    }

    public async Task<JsonDocument> CallAsync(string method, CancellationToken ct, params object?[] parameters)
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

        if (_logger?.IsEnabled(LogLevel.Information) is true)
            LogReceive(method, id, response);

        return response;
    }

    private void LogCall(string method, Guid id, byte[] json)
    {
        if (_logger?.IsEnabled(LogLevel.Trace) is true)
            _logger?.LogTrace("Calling '{Method}' ({Id})\r\n{Json}", method, id, Encoding.UTF8.GetString(json));
        else
            _logger?.LogInformation("Calling '{Method}' ({Id})", method, id);
    }

    private void LogReceive(string method, Guid id, JsonDocument json)
    {
        if (_logger?.IsEnabled(LogLevel.Trace) is true)
            _logger?.LogTrace("Response for '{Method}' ({Id})\r\n{Json}", method, id, JsonSerializer.Serialize(json, _jsonOptions));
        else
            _logger?.LogInformation("Response for '{Method}' ({Id})", method, id);
    }

    public async Task OpenAsync()
    {
        try
        {
            await _socket.ConnectAsync(Url, _cts.Token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to {Url}", Url);
            throw;
        }
        _logger?.LogInformation("Connection established with {Url}", Url);
        _connection = Task.Run(async () =>
        {
            WebSocketReceiveResult result;
            var buffer = new byte[2048];
            var segment = new ArraySegment<byte>(buffer);
            using var stream = new MemoryStream();
            do
            {
                stream.SetLength(0);
                do
                {
                    result = await _socket.ReceiveAsync(segment, _cts.Token);
                    stream.Write(buffer, segment.Offset, result.Count);

                } while (!result.EndOfMessage);

                if (result.MessageType is WebSocketMessageType.Close)
                    break;

                stream.Seek(0, SeekOrigin.Begin);
                var response = await JsonSerializer.DeserializeAsync<JsonDocument>(stream, _jsonOptions);

                if (response is null)
                    throw new InvalidOperationException("rpc response deserialized to null???");

                var isResult = response.RootElement.ValueKind is JsonValueKind.Object && response.RootElement.GetProperty("id").ValueKind is JsonValueKind.String;

                if (isResult)
                    HandleResult(response.RootElement.GetProperty("id").GetGuid(), response);
                else
                    await HandleNotificationAsync(response);

            } while (!_cts.IsCancellationRequested);
        });
    }

    private void HandleResult(Guid id, JsonDocument response)
    {
        var success = _responses.TryGetValue(id, out var token);
        Debug.Assert(success is true);
        token!.SetResponse(response);
    }

    private async Task HandleNotificationAsync(JsonDocument response)
    {
        var method = response.RootElement.GetProperty("method").GetString()!;
        _logger?.LogInformation("Notification {notification}", method);
        if (_notificationHandlers.TryGetValue(method, out var set))
            foreach (var handler in set) await handler();
    }

    public async Task CloseAsync()
    {
        _cts.Cancel();
        await _connection;
        _connection = Task.CompletedTask;
    }
}