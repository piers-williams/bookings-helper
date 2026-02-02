# OSM OAuth 2.0 Implementation Guide

## Overview

This document outlines how to implement OAuth 2.0 authentication for the OSM API integration.

## OSM OAuth Endpoints

From OSM documentation:
- **Authorization URL**: `https://www.onlinescoutmanager.co.uk/oauth/authorize`
- **Token URL**: `https://www.onlinescoutmanager.co.uk/oauth/token`
- **Resource Owner URL**: `https://www.onlinescoutmanager.co.uk/oauth/resource`

## OAuth Flow Types

### Option 1: Client Credentials Flow (Recommended for Single User)

Use this if you're the only user of the application.

**Advantages:**
- Simpler implementation
- No user interaction needed
- Application authenticates as your user account

**Steps:**
1. Register application in OSM
2. Get Client ID and Client Secret
3. Exchange credentials for access token
4. Store token with your user record
5. Refresh token when expired

### Option 2: Authorization Code Flow (For Multi-User)

Use this if multiple people will use the application.

**Advantages:**
- Each user authenticates separately
- Proper OAuth flow
- User consent screen

**Steps:**
1. Register application in OSM
2. User clicks "Sign in with OSM"
3. Redirect to OSM authorization URL
4. User logs in and grants permission
5. OSM redirects back with authorization code
6. Exchange code for access token + refresh token
7. Store tokens per user

## Required OAuth Scopes

For venue bookings management, request:
- `section:campsite_bookings:read` - Read booking data
- `section:campsite_bookings:write` - Add comments (Phase 2)

Full scope format: `section:campsite_bookings:read section:campsite_bookings:write`

## Implementation Tasks

### Task A: Register OSM Application

1. Log in to OSM: https://www.onlinescoutmanager.co.uk
2. Navigate to My Account → API Applications (or similar)
3. Create New Application:
   - **Name**: Bookings Assistant
   - **Description**: Scout campsite booking management
   - **Redirect URI**: `https://your-homeassistant:5001/api/auth/osm/callback`
   - **Scopes**: `section:campsite_bookings:read section:campsite_bookings:write`
4. Save Client ID and Client Secret

### Task B: Update Configuration

Update `appsettings.json`:
```json
{
  "Osm": {
    "BaseUrl": "https://www.onlinescoutmanager.co.uk",
    "CampsiteId": "219",
    "SectionId": "56710",
    "ClientId": "",
    "ClientSecret": "",
    "RedirectUri": "https://your-homeassistant:5001/api/auth/osm/callback",
    "Scopes": "section:campsite_bookings:read section:campsite_bookings:write"
  }
}
```

### Task C: Implement OsmAuthService

Create `Services/IOsmAuthService.cs`:
```csharp
public interface IOsmAuthService
{
    Task<string> GetValidAccessTokenAsync(int userId);
    Task<string> GetAuthorizationUrlAsync();
    Task<bool> HandleCallbackAsync(string code, int userId);
}
```

Create `Services/OsmAuthService.cs`:
- Implement token exchange (POST to /oauth/token)
- Store tokens in ApplicationUser table (add OsmAccessToken, OsmRefreshToken fields)
- Implement token refresh logic
- Use DataProtection for encryption

### Task D: Update AuthController

Add OSM OAuth endpoints to `AuthController.cs`:
```csharp
[HttpGet("osm/login")]
public IActionResult OsmLogin()
{
    var url = await _osmAuthService.GetAuthorizationUrlAsync();
    return Redirect(url);
}

[HttpGet("osm/callback")]
public async Task<IActionResult> OsmCallback([FromQuery] string code)
{
    var userId = 1; // TODO: Get from authenticated user
    var success = await _osmAuthService.HandleCallbackAsync(code, userId);
    return success ? Ok() : BadRequest("OAuth failed");
}
```

### Task E: Update OsmService

Modify `OsmService.cs` to use OAuth:
```csharp
private readonly IOsmAuthService _authService;

private async Task<string> GetAccessTokenAsync()
{
    var userId = 1; // TODO: Get from current user context
    return await _authService.GetValidAccessTokenAsync(userId);
}

public async Task<List<BookingDto>> GetBookingsAsync(string status)
{
    var token = await GetAccessTokenAsync();

    _httpClient.DefaultRequestHeaders.Clear();
    _httpClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    // Make API call...
}
```

### Task F: Update Database Schema

Add migration to add OSM OAuth fields to ApplicationUser:
```csharp
// Migration
public partial class AddOsmOAuthFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OsmAccessToken",
            table: "ApplicationUsers",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OsmRefreshToken",
            table: "ApplicationUsers",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "OsmTokenExpiry",
            table: "ApplicationUsers",
            nullable: true);
    }
}
```

## Testing OAuth Flow

### 1. Start Application
```bash
dotnet run --project BookingsAssistant.Api
```

### 2. Initiate OAuth
Navigate to: `http://localhost:5068/api/auth/osm/login`

### 3. Expected Flow
1. Redirects to OSM login page
2. You log in with OSM credentials
3. OSM shows consent screen
4. You approve the application
5. OSM redirects back to `/api/auth/osm/callback?code=...`
6. Application exchanges code for token
7. Token stored in database (encrypted)

### 4. Test API Call
```bash
curl http://localhost:5068/api/bookings
```

Should now return real booking data from OSM!

## Rate Limiting

OSM provides rate limit headers:
- `X-RateLimit-Limit` - Requests per hour per user
- `X-RateLimit-Remaining` - Requests remaining
- `X-RateLimit-Reset` - Seconds until reset

Our OsmService already logs these headers.

## Troubleshooting

### "Invalid Redirect URI"
- Check OSM application settings
- Ensure redirect URI exactly matches

### "Invalid Scope"
- Verify scope format: `section:campsite_bookings:read`
- Check you have permission for those scopes

### 401 Unauthorized
- Token may be expired
- Check token refresh logic
- Verify scopes are correct

### No Bookings Returned
- Check campsite ID and section ID are correct
- Verify your OSM account has access to that campsite

## Next Steps

After implementing OAuth:

1. **Test with real OSM data**
2. **Verify smart linking** works with real booking references
3. **Update front end** to show OAuth status
4. **Add token refresh** on expiration
5. **Implement Phase 2 features** (add comments with OAuth write scope)

## References

- OSM OAuth Documentation: (from browser DevTools exploration)
- OAuth 2.0 RFC: https://tools.ietf.org/html/rfc6749
- Microsoft OAuth Implementation: See `Office365Service.cs` for similar pattern
