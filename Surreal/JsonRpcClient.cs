using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Surreal;

public class JsonRpcClient
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ClientWebSocket _socket = new();
    private Task _connection = Task.CompletedTask;
    private readonly Uri _uri;
    private readonly ConcurrentDictionary<Guid, Token> _responses = new();
    private readonly ConcurrentDictionary<string, HashSet<Func<Task>>> _notificationHandlers = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public JsonRpcClient(Uri uri)
    {
        _uri = uri;
    }

    public void On(string notification, Func<Task> callback)
    {
        if (_notificationHandlers.TryGetValue(notification, out var set))
            set.Add(callback);
        else
            _notificationHandlers.TryAdd(notification, new HashSet<Func<Task>> { callback });
    }

    class Token
    {
        public required Guid Id { get; init; }

        private readonly TaskCompletionSource<JsonDocument> _tcs = new();

        public async Task<JsonDocument> GetAsync()
        {
            return await _tcs.Task;
        }

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
        Console.WriteLine("CALL");
        Console.WriteLine(Encoding.UTF8.GetString(jsonBytes));
        var token = new Token { Id = id };
        _responses.TryAdd(id, token);
        await _socket.SendAsync(jsonBytes, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, ct);
        var response = await token.GetAsync();
        _responses.Remove(id, out _);
        Console.WriteLine("RESPONSE");
        Console.WriteLine(JsonSerializer.Serialize(response, _jsonOptions));
        return response;
    }

    public async Task OpenAsync()
    {
        await _socket.ConnectAsync(_uri, _cts.Token);
        _connection = Task.Run(async () =>
        {
            WebSocketReceiveResult result;
            var buffer = new ArraySegment<byte>(new byte[2048]);
            do
            {
                using var stream = new MemoryStream();
                do
                {
                    result = await _socket.ReceiveAsync(buffer, _cts.Token);
                    stream.Write(buffer.Array!, buffer.Offset, result.Count);
                    await Task.Delay(17);

                } while (!result.EndOfMessage);

                if (result.MessageType is WebSocketMessageType.Close)
                    break;

                stream.Seek(0, SeekOrigin.Begin);
                var response = await JsonSerializer.DeserializeAsync<JsonDocument>(stream, _jsonOptions);

                if (response is null)
                    throw new InvalidOperationException("rpc response deserialized to null???");

                var isResult = response.RootElement.ValueKind is JsonValueKind.Object && response.RootElement.GetProperty("id").ValueKind is JsonValueKind.String;

                if (isResult)
                    await HandleResultAsync(response.RootElement.GetProperty("id").GetGuid(), response);
                else
                    await HandleNotificationAsync(response);

            } while (!_cts.IsCancellationRequested);
        });
    }

    private async Task HandleResultAsync(Guid id, JsonDocument response)
    {
        Console.WriteLine($"RESULT {id}");
        Console.WriteLine(JsonSerializer.Serialize(response, _jsonOptions));
        var success = _responses.TryGetValue(id, out var token);
        Debug.Assert(success is true);
        token!.SetResponse(response);
    }

    private async Task HandleNotificationAsync(JsonDocument response)
    {
        Console.WriteLine("NOTIFICATION");
        Console.WriteLine(JsonSerializer.Serialize(response, _jsonOptions));
        var method = response.RootElement.GetProperty("method").GetString()!;
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