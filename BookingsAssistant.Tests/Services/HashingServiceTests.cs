using BookingsAssistant.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingsAssistant.Tests.Services;

public class HashingServiceTests
{
    private static IHashingService Create() => new HashingService(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hashing:Iterations"] = "1",
                ["Hashing:SecretPath"] = "/nonexistent/path/secret.txt"
            })
            .Build(),
        NullLogger<HashingService>.Instance);

    [Fact]
    public void HashValue_IsDeterministic()
    {
        var svc = Create();
        Assert.Equal(svc.HashValue("test@example.com"), svc.HashValue("test@example.com"));
    }

    [Fact]
    public void HashValue_NormalisesCase()
    {
        var svc = Create();
        Assert.Equal(svc.HashValue("Test@Example.COM"), svc.HashValue("test@example.com"));
    }

    [Fact]
    public void HashValue_NormalisesWhitespace()
    {
        var svc = Create();
        Assert.Equal(svc.HashValue("  test@example.com  "), svc.HashValue("test@example.com"));
    }

    [Fact]
    public void HashValue_DifferentInputsProduceDifferentHashes()
    {
        var svc = Create();
        Assert.NotEqual(svc.HashValue("a@example.com"), svc.HashValue("b@example.com"));
    }

    [Fact]
    public void HashValue_Returns64CharLowercaseHex()
    {
        var svc = Create();
        var hash = svc.HashValue("test@example.com");
        Assert.Equal(64, hash.Length);
        Assert.All(hash, c => Assert.True(c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }
}
