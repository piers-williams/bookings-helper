using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IOsmAuthService _osmAuthService;
    private readonly IOffice365Service _office365Service;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ITokenService tokenService,
        IOsmAuthService osmAuthService,
        IOffice365Service office365Service,
        ILogger<AuthController> logger)
    {
        _tokenService = tokenService;
        _osmAuthService = osmAuthService;
        _office365Service = office365Service;
        _logger = logger;
    }

    /// <summary>
    /// Initiates OAuth login flow with Microsoft Office 365.
    /// Redirects to Microsoft login page.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] bool adminConsent = false)
    {
        _logger.LogInformation("Office 365 login endpoint called (admin consent: {AdminConsent})", adminConsent);

        if (adminConsent)
        {
            var adminUrl = _office365Service.GetAdminConsentUrl();
            return Redirect(adminUrl);
        }

        var url = _office365Service.GetAuthorizationUrl();
        return Redirect(url);
    }

    /// <summary>
    /// OAuth callback endpoint - receives authorization code from Microsoft.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("Office 365 callback received without authorization code");
            return BadRequest("Authorization code missing");
        }

        _logger.LogInformation("Office 365 callback endpoint called with code");

        var userId = 1; // TODO: Get from authenticated user session
        var success = await _office365Service.HandleCallbackAsync(code, userId);

        if (success)
        {
            _logger.LogInformation("Office 365 OAuth authorization successful for user {UserId}", userId);
            return Redirect("/"); // Redirect to dashboard
        }
        else
        {
            _logger.LogError("Office 365 OAuth authorization failed for user {UserId}", userId);
            return BadRequest("OAuth authorization failed");
        }
    }

    /// <summary>
    /// Returns the current Office 365 authentication status.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        _logger.LogInformation("Office 365 status endpoint called");

        var userId = 1; // TODO: Get from authenticated user
        // Check if user has valid Office 365 token
        try
        {
            await _office365Service.GetValidAccessTokenAsync(userId);
            return Ok(new { authenticated = true });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "User {UserId} does not have valid Office 365 token", userId);
            return Ok(new { authenticated = false });
        }
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
