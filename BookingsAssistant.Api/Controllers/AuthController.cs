using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ITokenService tokenService, ILogger<AuthController> logger)
    {
        _tokenService = tokenService;
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
}
