using System.Security.Cryptography;
using System.Text;

namespace BookingsAssistant.Api.Services;

public class HashingService : IHashingService
{
    private readonly byte[] _secret;
    private readonly int _iterations;

    public HashingService(IConfiguration configuration, ILogger<HashingService> logger)
    {
        _iterations = configuration.GetValue<int>("Hashing:Iterations", 200_000);

        var secretPath = configuration["Hashing:SecretPath"] ?? "/data/hash-secret.txt";

        if (File.Exists(secretPath))
        {
            _secret = Convert.FromHexString(File.ReadAllText(secretPath).Trim());
            logger.LogInformation("Loaded hash secret from {Path}", secretPath);
        }
        else if (Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(secretPath))))
        {
            _secret = RandomNumberGenerator.GetBytes(32);
            File.WriteAllText(secretPath, Convert.ToHexString(_secret));
            logger.LogInformation("Generated and saved new hash secret to {Path}", secretPath);
        }
        else
        {
            // Development / test: deterministic fallback — never used in production
            _secret = Encoding.UTF8.GetBytes("dev-fallback-secret-do-not-use-in-production!!!");
            logger.LogWarning("Hash secret path {Path} not accessible, using development fallback", secretPath);
        }
    }

    public string HashValue(string value)
    {
        var normalized = value.ToLowerInvariant().Trim();
        var passwordBytes = Encoding.UTF8.GetBytes(normalized);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: passwordBytes,
            salt: _secret,
            iterations: _iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
