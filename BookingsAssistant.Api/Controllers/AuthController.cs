using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IOsmAuthService _osmAuthService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ITokenService tokenService, IOsmAuthService osmAuthService, ILogger<AuthController> logger)
    {
        _tokenService = tokenService;
        _osmAuthService = osmAuthService;
        _logger = logger;
    }

    /// <summary>
    /// Initiates OAuth login flow with Microsoft Office 365.
    /// This is a stub - will be implemented after Azure AD app registration.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login()
    {
        _logger.LogInformation("Login endpoint called - OAuth flow not yet implemented");

        // TODO: Implement OAuth flow after Azure AD app registration
        // This will redirect to Microsoft login page
        // return Challenge(new AuthenticationProperties { RedirectUri = "/api/auth/callback" }, "Microsoft");

        return StatusCode(501, new
        {
            message = "OAuth login flow not yet implemented",
            detail = "This endpoint will be implemented after Azure AD app is registered"
        });
    }

    /// <summary>
    /// OAuth callback endpoint - receives authorization code from Microsoft.
    /// This is a stub - will be implemented after Azure AD app registration.
    /// </summary>
    [HttpGet("callback")]
    public IActionResult Callback([FromQuery] string? code)
    {
        _logger.LogInformation("Callback endpoint called with code: {HasCode}", !string.IsNullOrEmpty(code));

        // TODO: Implement OAuth callback after Azure AD app registration
        // This will exchange the code for access and refresh tokens
        // Then save tokens using _tokenService.SaveTokensAsync()

        return StatusCode(501, new
        {
            message = "OAuth callback not yet implemented",
            detail = "This endpoint will be implemented after Azure AD app is registered",
            receivedCode = !string.IsNullOrEmpty(code)
        });
    }

    /// <summary>
    /// Returns the current authentication status.
    /// This is a stub - returns false until OAuth is implemented.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        _logger.LogInformation("Status endpoint called");

        // TODO: Check if user has valid tokens
        // For now, always return not authenticated
        return Ok(new
        {
            isAuthenticated = false,
            message = "OAuth authentication not yet implemented"
        });
    }

    /// <summary>
    /// Logs out the user by clearing their tokens.
    /// This is a stub - will be implemented after OAuth is complete.
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _logger.LogInformation("Logout endpoint called");

        // TODO: Clear user's tokens from database
        // For now, just return success
        return Ok(new
        {
            message = "Logout successful",
            detail = "Token clearing not yet implemented"
        });
    }

    /// <summary>
    /// Initiates OAuth login flow with OSM.
    /// Redirects to OSM authorization page.
    /// </summary>
    [HttpGet("osm/login")]
    public IActionResult OsmLogin()
    {
        _logger.LogInformation("OSM login endpoint called");
        var url = _osmAuthService.GetAuthorizationUrl();
        return Redirect(url);
    }

    /// <summary>
    /// OAuth callback endpoint - receives authorization code from OSM.
    /// </summary>
    [HttpGet("osm/callback")]
    public async Task<IActionResult> OsmCallback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("OSM callback received without authorization code");
            return BadRequest("Authorization code missing");
        }

        _logger.LogInformation("OSM callback endpoint called with code");

        var userId = 1; // TODO: Get from authenticated user session
        var success = await _osmAuthService.HandleCallbackAsync(code, userId);

        if (success)
        {
            _logger.LogInformation("OSM OAuth authorization successful for user {UserId}", userId);
            return Redirect("/"); // Redirect to dashboard
        }
        else
        {
            _logger.LogError("OSM OAuth authorization failed for user {UserId}", userId);
            return BadRequest("OAuth authorization failed");
        }
    }

    /// <summary>
    /// Returns the current OSM authentication status.
    /// </summary>
    [HttpGet("osm/status")]
    public async Task<IActionResult> OsmStatus()
    {
        _logger.LogInformation("OSM status endpoint called");

        var userId = 1; // TODO: Get from authenticated user
        // Check if user has valid OSM token
        try
        {
            await _osmAuthService.GetValidAccessTokenAsync(userId);
            return Ok(new { authenticated = true });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "User {UserId} does not have valid OSM token", userId);
            return Ok(new { authenticated = false });
        }
    }
}
