using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IOsmAuthService _osmAuthService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IOsmAuthService osmAuthService, ILogger<AuthController> logger)
    {
        _osmAuthService = osmAuthService;
        _logger = logger;
    }

    [HttpGet("osm/login")]
    public IActionResult OsmLogin()
    {
        _logger.LogInformation("OSM login endpoint called");
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/osm/callback";
        var url = _osmAuthService.GetAuthorizationUrl(redirectUri);
        return Redirect(url);
    }

    [HttpGet("osm/callback")]
    public async Task<IActionResult> OsmCallback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Authorization code missing");

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/osm/callback";
        var success = await _osmAuthService.HandleCallbackAsync(code, 1, redirectUri);

        return success ? Redirect("/") : BadRequest("OAuth authorization failed");
    }

    [HttpGet("osm/status")]
    public async Task<IActionResult> OsmStatus()
    {
        try
        {
            await _osmAuthService.GetValidAccessTokenAsync(1);
            return Ok(new { authenticated = true });
        }
        catch
        {
            return Ok(new { authenticated = false });
        }
    }
}
