using System.Text.Json;

namespace Surreal;

public class SurrealConnection
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

