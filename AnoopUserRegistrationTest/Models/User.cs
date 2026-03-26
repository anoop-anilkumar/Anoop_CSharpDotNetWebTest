using System.ComponentModel.DataAnnotations;

namespace AnoopUserRegistrationTest.Models;

public class User
{
    public const string DefaultRole = "User";

    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(ValidationRules.NamePattern)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(ValidationRules.NamePattern)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = DefaultRole;

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockoutEndUtc { get; set; }
}
