using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Surreal;

var services = new ServiceCollection();

services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Trace);
});
services.AddSurrealDB("localhost:8000");

var di = services.BuildServiceProvider();

var surreal = di.GetRequiredService<SurrealConnection>();
await surreal.OpenAsync();
await surreal.SignInAsync("root", "root");
await surreal.UseAsync("test", "test");
await surreal.QueryAsync("info for kv"); // Gets information about the current? namespace
