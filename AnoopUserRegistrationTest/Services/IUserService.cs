using AnoopUserRegistrationTest.Models;

namespace AnoopUserRegistrationTest.Services;

public interface IUserService
{
    Task<(bool Success, string ErrorMessage)> RegisterUserAsync(RegisterViewModel model);
    Task<AuthenticationResult> AuthenticateAsync(LoginViewModel model);
    Task<User?> GetUserByEmailAsync(string email);
    Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(ForgotPasswordViewModel model);
    Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(string email, ChangePasswordViewModel model);
}
