using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

public class ApiTokenPolicyTests
{
    private const string Token = "s3cr3t-token";

    [Theory]
    [InlineData("/api/bookings")]
    [InlineData("/api/emails/capture")]
    [InlineData("/api/bookings/sync")]
    public void NoConfiguredToken_AllowsEverything(string path)
        => Assert.True(ApiTokenPolicy.IsAllowed(path, configuredToken: "", providedToken: null));

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/assets/index-abc.js")]
    [InlineData("/bookings-extension.zip")]
    public void NonApiPaths_AreAlwaysAllowed(string path)
        => Assert.True(ApiTokenPolicy.IsAllowed(path, Token, providedToken: null));

    [Theory]
    [InlineData("/api/auth/osm/login")]
    [InlineData("/api/auth/osm/callback")]
    [InlineData("/api/auth/osm/status")]
    public void AuthPaths_AreExempt_EvenWithoutToken(string path)
        => Assert.True(ApiTokenPolicy.IsAllowed(path, Token, providedToken: null));

    [Theory]
    [InlineData("/api/bookings")]
    [InlineData("/api/emails/capture")]
    [InlineData("/api/links")]
    public void ApiPaths_AreBlocked_WithoutToken(string path)
        => Assert.False(ApiTokenPolicy.IsAllowed(path, Token, providedToken: null));

    [Fact]
    public void ApiPath_Blocked_WithWrongToken()
        => Assert.False(ApiTokenPolicy.IsAllowed("/api/bookings", Token, "wrong"));

    [Fact]
    public void ApiPath_Allowed_WithCorrectToken()
        => Assert.True(ApiTokenPolicy.IsAllowed("/api/bookings", Token, Token));

    [Fact]
    public void ApiPath_Blocked_WithEmptyProvidedToken()
        => Assert.False(ApiTokenPolicy.IsAllowed("/api/bookings", Token, ""));

    [Fact]
    public void LookalikePrefix_IsNotTreatedAsApi()
        // "/apixyz" must not be guarded as if it were under /api.
        => Assert.True(ApiTokenPolicy.IsAllowed("/apixyz", Token, providedToken: null));
}
