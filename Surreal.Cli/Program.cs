using Surreal;

var surreal = new SurrealConnection("localhost:8000");
await surreal.OpenAsync();
var signedIn = await surreal.SignInAsync("root", "root");

if (signedIn)
    Console.WriteLine("Signed in as root");
else
    Console.WriteLine("Failed to sign in");

Console.ReadLine();
