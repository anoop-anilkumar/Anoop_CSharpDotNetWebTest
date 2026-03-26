namespace AnoopUserRegistrationTest.Models;

public class AuthenticationResult
{
    public bool Success => User is not null;

    public User? User { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
