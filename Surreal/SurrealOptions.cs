using System.Net.WebSockets;

using Microsoft.Extensions.DependencyInjection;

namespace Surreal;

public sealed class SurrealOptions
{
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// The url of the SurrealDB database server.
    /// </summary>
    public string Url { get; set; } = default!;

    public Action<ClientWebSocketOptions>? ConfigureRpc { get; set; } = default;

    /// <summary>
    /// Use an encrypted TLS connection or not. Defaults to <see langword="false"/>.
    /// <code>
    /// If set to <see langword="true"/>, the websocket connection will be made over wss://
    /// If set to <see langword="false"/>, the websocket connection will be made over ws://
    /// </code>
    /// </summary>
    public bool EncryptedConnection { get; set; } = false;

    private string Protocol => EncryptedConnection ? "wss" : "ws";

    public Uri ComputedUri => new($"{Protocol}://{Url}/rpc");
}
