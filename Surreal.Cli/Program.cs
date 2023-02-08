using System.Text.Json;

using Humanizer;

using JsonRpc;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;

using Surreal;
using Surreal.Cli;

Log.Logger = CreateLogger();
var app = CreateHost(args);

var surreal = app.Services.GetRequiredService<SurrealConnection>();
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

static IHost CreateHost(string[] args)
{
    return new HostBuilder()
        .ConfigureServices(services =>
        {
            services.AddJsonRpc();
            services.AddSurrealDB("localhost:8000", options => options.EncryptedConnection = false);
        })
        .UseSerilog()
        .Build();
}

static Logger CreateLogger()
{
    return new LoggerConfiguration()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss}] ({Level}) {NewLine}{Message:lj}{NewLine}{NewLine}", theme: new HslConsoleTheme(new Dictionary<ConsoleThemeStyle, Hsl>
        {
            [ConsoleThemeStyle.Null] = new Hsl(276, 100, 49),
            [ConsoleThemeStyle.Name] = new Hsl(125, 100, 49),
            [ConsoleThemeStyle.LevelVerbose] = new Hsl(0, 0, 20),
            [ConsoleThemeStyle.LevelInformation] = new Hsl(180, 100, 50),
        }))
        .MinimumLevel.Verbose()
        .Destructure.ByTransforming<TimeSpan>(ts => ts.Humanize(int.MaxValue))
        .CreateLogger();
}
