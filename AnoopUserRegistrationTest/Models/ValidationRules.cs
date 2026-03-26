namespace AnoopUserRegistrationTest.Models;

public static class ValidationRules
{
    public const string NamePattern = @"^[A-Za-z][A-Za-z\s'-]*$";
    public const string EmailPattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";
    public const string EmailMessage = "Please enter a valid email address.";
    public const string StrongPasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$";
    public const string StrongPasswordMessage = "Password must be at least 8 characters and include an uppercase letter, lowercase letter, number, and special character.";
}
