namespace Surreal.Cli;

record User
{
    public required string UserName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}

