using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var surreal = new SurrealConnection("localhost:8000");
await surreal.OpenAsync();
var signedIn = await surreal.SignInAsync("root", "root");

if (signedIn)
    Console.WriteLine("Signed in as root");
else
    Console.WriteLine("Failed to sign in");

Console.ReadLine();



class SurrealConnection
{
    private readonly Uri _url;
    private readonly JsonRpcClient _rpc;

    public SurrealConnection(string url)
    {
        _url = new Uri($"ws://{url}/rpc");
        _rpc = new(_url);
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
}

class JsonRpcClient
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ClientWebSocket _socket = new();
    private Task _connection = Task.CompletedTask;
    private readonly Uri _uri;
    private readonly ConcurrentDictionary<Guid, JsonDocument> _responses = new();
    private readonly ConcurrentDictionary<string, Func<Task>> _notificationHandlers = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public JsonRpcClient(Uri uri)
    {
        _uri = uri;
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
        await _socket.SendAsync(jsonBytes, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, ct);
        var response = WaitForResponse(id, ct);
        Console.WriteLine("RESPONSE");
        Console.WriteLine(JsonSerializer.Serialize(response, _jsonOptions));
        return response;
    }

    private JsonDocument WaitForResponse(Guid id, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_responses.TryRemove(id, out var value))
                return value;
        }

        throw new OperationCanceledException("The response was cancelled", ct);
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

    // TODO: Use TaskCompletionSource to pass the result from the inner connection loop to the call site

    private async Task HandleResultAsync(Guid id, JsonDocument response)
    {
        Console.WriteLine($"RESULT {id}");
        Console.WriteLine(JsonSerializer.Serialize(response, _jsonOptions));
        _responses.TryAdd(Guid.Parse(response.RootElement.GetProperty("id").GetString()!), response);
    }

    private async Task HandleNotificationAsync(JsonDocument response)
    {
        Console.WriteLine("NOTIFICATION");
        Console.WriteLine(JsonSerializer.Serialize(response, _jsonOptions));
        var method = response.RootElement.GetProperty("method").GetString()!;
        if (_notificationHandlers.ContainsKey(method))
            await _notificationHandlers[method]();
    }

    public async Task CloseAsync()
    {
        _cts.Cancel();
        await _connection;
        _connection = Task.CompletedTask;
    }
}