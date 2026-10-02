using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SuperShortLink;

public sealed class AdminCredentialService
{
    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly string _initialPassword;
    private readonly PasswordHasher<string> _hasher = new();
    private string? _passwordHash;

    public AdminCredentialService(IOptions<ShortLinkOptions> options, IConfiguration configuration, IWebHostEnvironment environment)
    {
        UserName = options.Value.LoginAcount;
        _initialPassword = options.Value.LoginPassword;
        _filePath = configuration["AdminCredentialPath"]
            ?? Path.Combine(environment.ContentRootPath, ".local", "admin-password.hash");
        if (File.Exists(_filePath))
        {
            _passwordHash = File.ReadAllText(_filePath).Trim();
            if (string.IsNullOrWhiteSpace(_passwordHash))
                throw new InvalidOperationException("The administrator password file is empty.");
        }
    }

    public string UserName { get; }

    public bool Verify(string? userName, string? password)
    {
        if (userName != UserName || string.IsNullOrEmpty(password)) return false;
        lock (_sync) return VerifyPassword(password);
    }

    public string Version
    {
        get
        {
            lock (_sync)
            {
                var current = _passwordHash ?? _initialPassword;
                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(current)));
            }
        }
    }

    public bool ChangePassword(string? currentPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword)) return false;
        lock (_sync)
        {
            if (!VerifyPassword(currentPassword)) return false;

            var hash = _hasher.HashPassword(UserName, newPassword);
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $".admin-password-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, hash);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temporaryPath, _filePath, true);
                _passwordHash = hash;
                return true;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }

    private bool VerifyPassword(string password)
    {
        if (_passwordHash is not null)
            return _hasher.VerifyHashedPassword(UserName, _passwordHash, password) != PasswordVerificationResult.Failed;

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(_initialPassword));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
