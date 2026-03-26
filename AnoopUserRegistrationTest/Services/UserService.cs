using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnoopUserRegistrationTest.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AnoopUserRegistrationTest.Services;

public class UserService : IUserService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    private readonly ILogger<UserService> _logger;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly string _storagePath;
    private readonly byte[] _encryptionKey;

    public UserService(
        IWebHostEnvironment environment,
        ILogger<UserService> logger,
        IPasswordHasher<User> passwordHasher,
        IOptions<StorageEncryptionOptions> encryptionOptions)
    {
        _logger = logger;
        _passwordHasher = passwordHasher;
        _storagePath = Path.Combine(environment.ContentRootPath, "App_Data", "users.json");
        _encryptionKey = GetEncryptionKey(encryptionOptions.Value);
    }

    public async Task<(bool Success, string ErrorMessage)> RegisterUserAsync(RegisterViewModel model)
    {
        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        await FileLock.WaitAsync();

        try
        {
            var users = await LoadUsersAsync();
            if (users.Any(user => user.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Registration rejected for duplicate email {Email}.", normalizedEmail);
                return (false, "An account with this email already exists.");
            }

            var user = new User
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = normalizedEmail,
                Role = User.DefaultRole,
                FailedLoginAttempts = 0,
                LockoutEndUtc = null
            };

            user.PasswordHash = HashPassword(model.Password);
            users.Add(user);

            await SaveUsersAsync(users);
            _logger.LogInformation("User {Email} registered successfully.", normalizedEmail);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for email {Email}.", normalizedEmail);
            return (false, "We couldn't complete registration right now. Please try again.");
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<AuthenticationResult> AuthenticateAsync(LoginViewModel model)
    {
        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        await FileLock.WaitAsync();

        try
        {
            var users = await LoadUsersAsync();
            var user = users.FirstOrDefault(existing => existing.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                _logger.LogWarning("Login failed for unknown email {Email}.", normalizedEmail);
                return new AuthenticationResult { ErrorMessage = "The email or password you entered is incorrect." };
            }

            if (user.LockoutEndUtc is not null && user.LockoutEndUtc > DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Login blocked for locked out email {Email}.", normalizedEmail);
                return new AuthenticationResult
                {
                    ErrorMessage = $"This account is temporarily locked. Please try again after {user.LockoutEndUtc.Value.LocalDateTime:t}."
                };
            }

            var passwordVerification = VerifyPassword(user, model.Password);
            if (!passwordVerification.Success)
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    user.FailedLoginAttempts = 0;
                    user.LockoutEndUtc = DateTimeOffset.UtcNow.Add(LockoutDuration);
                    await SaveUsersAsync(users);

                    _logger.LogWarning("Login lockout applied for email {Email}.", normalizedEmail);
                    return new AuthenticationResult
                    {
                        ErrorMessage = $"Too many failed login attempts. The account is locked for {LockoutDuration.TotalMinutes:0} minutes."
                    };
                }

                await SaveUsersAsync(users);
                _logger.LogWarning("Login failed for email {Email} because of invalid credentials.", normalizedEmail);
                return new AuthenticationResult { ErrorMessage = "The email or password you entered is incorrect." };
            }

            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;

            if (passwordVerification.ShouldUpgradeHash)
            {
                user.PasswordHash = HashPassword(model.Password);
            }

            await SaveUsersAsync(users);

            _logger.LogInformation("User {Email} logged in successfully.", normalizedEmail);
            return new AuthenticationResult { User = user };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for email {Email}.", normalizedEmail);
            return new AuthenticationResult { ErrorMessage = "We couldn't sign you in right now. Please try again." };
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        await FileLock.WaitAsync();

        try
        {
            var users = await LoadUsersAsync();
            return users.FirstOrDefault(user => user.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load user details for email {Email}.", normalizedEmail);
            return null;
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(ForgotPasswordViewModel model)
    {
        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        await FileLock.WaitAsync();

        try
        {
            var users = await LoadUsersAsync();
            var user = users.FirstOrDefault(existing => existing.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                _logger.LogWarning("Password reset requested for unknown email {Email}.", normalizedEmail);
                return (false, "We couldn't find an account with that email address.");
            }

            user.PasswordHash = HashPassword(model.NewPassword);
            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;
            await SaveUsersAsync(users);

            _logger.LogInformation("Password reset completed for email {Email}.", normalizedEmail);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password reset failed for email {Email}.", normalizedEmail);
            return (false, "We couldn't reset the password right now. Please try again.");
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(string email, ChangePasswordViewModel model)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        await FileLock.WaitAsync();

        try
        {
            var users = await LoadUsersAsync();
            var user = users.FirstOrDefault(existing => existing.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                _logger.LogWarning("Change password requested for unknown email {Email}.", normalizedEmail);
                return (false, "We couldn't find your account. Please log in again.");
            }

            var passwordVerification = VerifyPassword(user, model.CurrentPassword);
            if (!passwordVerification.Success)
            {
                _logger.LogWarning("Change password rejected for email {Email} because the current password was invalid.", normalizedEmail);
                return (false, "Your current password is incorrect.");
            }

            user.PasswordHash = HashPassword(model.NewPassword);
            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;
            await SaveUsersAsync(users);

            _logger.LogInformation("Password changed successfully for email {Email}.", normalizedEmail);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change password failed for email {Email}.", normalizedEmail);
            return (false, "We couldn't change the password right now. Please try again.");
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<List<User>> LoadUsersAsync()
    {
        EnsureStorageDirectory();

        if (!File.Exists(_storagePath))
        {
            return [];
        }

        var storedPayload = await File.ReadAllTextAsync(_storagePath);
        var (decryptedJson, wasEncrypted) = TryDecryptStoredPayload(storedPayload);
        var users = JsonSerializer.Deserialize<List<User>>(decryptedJson);
        var loadedUsers = users ?? [];

        foreach (var user in loadedUsers.Where(user => string.IsNullOrWhiteSpace(user.Role)))
        {
            user.Role = User.DefaultRole;
        }

        if (!wasEncrypted && loadedUsers.Count > 0)
        {
            await SaveUsersAsync(loadedUsers);
        }

        return loadedUsers;
    }

    private async Task SaveUsersAsync(List<User> users)
    {
        EnsureStorageDirectory();

        // Passwords stay hashed, and the file itself is encrypted before it is written to disk.
        var json = JsonSerializer.Serialize(users, JsonOptions);
        var encryptedPayload = Encrypt(json);
        await File.WriteAllTextAsync(_storagePath, encryptedPayload);
    }

    private void EnsureStorageDirectory()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    // New passwords use a salted SHA256 format to match the assessment brief without needing an external package.
    private static string HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var salt = Convert.ToBase64String(saltBytes);
        var hash = ComputeSha256Hash(salt, password);

        return $"sha256${salt}${hash}";
    }

    private (bool Success, bool ShouldUpgradeHash) VerifyPassword(User user, string password)
    {
        if (IsSha256Password(user.PasswordHash))
        {
            return (VerifySha256Password(user.PasswordHash, password), false);
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return (verificationResult != PasswordVerificationResult.Failed, verificationResult != PasswordVerificationResult.Failed);
    }

    private static bool IsSha256Password(string passwordHash)
    {
        return passwordHash.StartsWith("sha256$", StringComparison.Ordinal);
    }

    private static bool VerifySha256Password(string storedHash, string password)
    {
        var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        var expectedHash = parts[2];
        var actualHash = ComputeSha256Hash(parts[1], password);

        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        var actualBytes = Encoding.UTF8.GetBytes(actualHash);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string ComputeSha256Hash(string salt, string password)
    {
        var inputBytes = Encoding.UTF8.GetBytes($"{salt}:{password}");
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToBase64String(hashBytes);
    }

    // AES-256-CBC protects the JSON file at rest; a fresh IV is generated for every save.
    private string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(payload);
    }

    private string Decrypt(string cipherText)
    {
        var payload = Convert.FromBase64String(cipherText);
        if (payload.Length <= 16)
        {
            throw new CryptographicException("The encrypted storage payload is invalid.");
        }

        var iv = payload[..16];
        var cipherBytes = payload[16..];

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] GetEncryptionKey(StorageEncryptionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new InvalidOperationException("Storage encryption key is missing.");
        }

        var keyBytes = Convert.FromBase64String(options.Key);
        if (keyBytes.Length != 32)
        {
            throw new InvalidOperationException("Storage encryption key must be 32 bytes for AES-256.");
        }

        return keyBytes;
    }

    // This keeps older plain JSON demo data readable and lets the next save rewrite it in encrypted form.
    private (string Json, bool WasEncrypted) TryDecryptStoredPayload(string storedPayload)
    {
        try
        {
            return (Decrypt(storedPayload), true);
        }
        catch (FormatException)
        {
            return (storedPayload, false);
        }
        catch (CryptographicException)
        {
            return (storedPayload, false);
        }
    }
}
