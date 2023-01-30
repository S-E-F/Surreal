using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Surreal;

var services = new ServiceCollection();

services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Trace);
});
services.AddSurrealDB("localhost:8000", options =>
{
    options.EncryptedConnection = true;
    options.ConfigureRpc = rpc =>
    {
    };
});

var di = services.BuildServiceProvider();

var surreal = di.GetRequiredService<SurrealConnection>();
await surreal.OpenAsync();
await surreal.SignInAsync("root", "root");
await surreal.UseAsync("test", "test");

var sef = new User
{
    UserName = "sef",
    DateOfBirth = new DateOnly(1993, 12, 30),
    FirstName = "Severin",
    LastName = "Fitriyadi"
};

await surreal.CreateAsync("users:sef", sef);
var result = await surreal.SelectAsync<User>("users:sef");

Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
}));

record User
{
    public required string UserName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}