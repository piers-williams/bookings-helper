# Bookings Assistant - Deployment Guide

This guide covers deploying the Bookings Assistant application in production environments.

## Prerequisites

- Azure account (free tier sufficient) for Azure AD app registration
- Office 365 account with access to shared bookings mailbox
- OSM account with API credentials
- Home Assistant instance (for addon deployment) OR Docker host (for standalone deployment)

## Table of Contents

1. [Azure AD App Registration](#azure-ad-app-registration)
2. [OSM API Credentials](#osm-api-credentials)
3. [Home Assistant Addon Installation](#home-assistant-addon-installation)
4. [Docker Standalone Deployment](#docker-standalone-deployment)
5. [Configuration Guide](#configuration-guide)
6. [Troubleshooting](#troubleshooting)

---

## Azure AD App Registration

The application uses Microsoft Graph API to access Office 365 emails. You need to register an application in Azure AD to obtain OAuth credentials.

### Step 1: Create Azure Account

1. Go to [https://portal.azure.com](https://portal.azure.com)
2. Sign in with personal Microsoft account (or create one)
3. Azure offers free tier - no payment required for basic app registration

### Step 2: Register Application

1. Navigate to **Azure Active Directory** (search in top bar)
2. Click **App registrations** in left sidebar
3. Click **+ New registration**

### Step 3: Configure Application

Fill in the registration form:

**Name:** `Bookings Assistant`

**Supported account types:** Select:
```
Accounts in any organizational directory (Any Azure AD directory - Multitenant)
and personal Microsoft accounts (e.g. Skype, Xbox)
```

**Redirect URI:**
- Platform: `Web`
- URI: `https://your-homeassistant-domain:8099/signin-oidc`
  - Replace `your-homeassistant-domain` with your actual Home Assistant URL
  - Or use `http://localhost:5000/signin-oidc` for local development

Click **Register**.

### Step 4: Create Client Secret

1. After registration, you'll see the app's Overview page
2. Copy the **Application (client) ID** - you'll need this later
3. Click **Certificates & secrets** in left sidebar
4. Click **+ New client secret**
5. Description: `Bookings Assistant Secret`
6. Expires: `24 months` (or custom period)
7. Click **Add**
8. **IMPORTANT:** Copy the **Value** immediately - it won't be shown again
9. Store both Client ID and Client Secret securely

### Step 5: Configure API Permissions

1. Click **API permissions** in left sidebar
2. You should see `User.Read` already added
3. Click **+ Add a permission**
4. Click **Microsoft Graph**
5. Click **Delegated permissions**
6. Search and add these permissions:
   - `Mail.Read` - Read user mail
   - `Mail.ReadWrite` - Read and write user mail (for Phase 2 features)
7. Click **Add permissions**

### Step 6: Grant Admin Consent (Optional)

If you're the admin of the Azure AD tenant:
1. Click **Grant admin consent for [Your Organization]**
2. Click **Yes**

This step is optional but provides better user experience by not requiring individual consent.

### Step 7: Note Your Credentials

You now have:
- **Application (client) ID**: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
- **Client Secret**: `your-secret-value`
- **Redirect URI**: The URL you configured

Keep these secure - you'll need them for configuration.

---

## OSM API Credentials

OSM (Online Scout Manager) doesn't have official public API documentation. The application uses reverse-engineered API endpoints.

### Option 1: Browser-Based Token Extraction

1. **Log in to OSM** via web browser (https://www.onlinescoutmanager.co.uk)
2. **Open Browser DevTools** (F12 or right-click → Inspect)
3. **Go to Network tab**
4. **Perform an action** in OSM (e.g., view bookings, open a booking)
5. **Find API requests** in Network tab (look for XHR/Fetch requests)
6. **Inspect request headers** - look for:
   - `Authorization` header
   - `X-API-Key` header
   - Cookie values
7. **Document the authentication mechanism**:
   - If using API key: Copy the key value
   - If using session token: Note the cookie name and value
   - If using Bearer token: Copy the token

### Option 2: Contact OSM Support

For official API access:
1. Contact OSM support: support@onlinescoutmanager.co.uk
2. Request API access for programmatic integration
3. Provide use case: "Automated campsite booking management"

### Current Implementation Status

**Note:** The current implementation includes a stub `OsmService` with basic API endpoint structure. The actual authentication and endpoints are documented in `docs/osm-api-discovery.md`.

Based on OSM API exploration:
- **Base URL**: `https://www.onlinescoutmanager.co.uk`
- **Authentication**: Session-based (cookies) or API token (TBD)
- **Key Endpoints**:
  - `/ext/bookings/bookings/` - Get bookings
  - `/ext/bookings/booking/?action=getBooking&bookingid={id}` - Get booking details
  - Additional endpoints documented in discovery file

---

## Home Assistant Addon Installation

The recommended deployment method for home users.

### Prerequisites

- Home Assistant OS or Home Assistant Supervised
- SSH access to Home Assistant (optional but recommended)

### Step 1: Prepare Configuration

Before installation, prepare these values:
- Azure Client ID (from Azure AD registration)
- Azure Client Secret (from Azure AD registration)
- Azure Redirect URI (should match what you configured in Azure AD)
- OSM Base URL: `https://www.onlinescoutmanager.co.uk`
- OSM API credentials (from previous section)

### Step 2: Install Addon

**Option A: Local Addon Installation**

1. Copy the entire project directory to Home Assistant:
   ```bash
   # From your development machine
   scp -r BookingsAssistant root@homeassistant.local:/addons/bookings-assistant
   ```

2. In Home Assistant UI:
   - Go to **Settings** → **Add-ons**
   - Click **Add-on Store**
   - Click **⋮** (three dots) in top right
   - Click **Check for updates**
   - You should see "Bookings Assistant" in local addons

**Option B: Git Repository Installation**

1. If you have a Git repository hosting the addon:
   - Go to **Settings** → **Add-ons** → **Add-on Store**
   - Click **⋮** → **Repositories**
   - Add repository URL
   - Click **Add**
   - Find addon in list and click to install

### Step 3: Configure Addon

1. Click **Bookings Assistant** addon
2. Go to **Configuration** tab
3. Fill in the values:
   ```yaml
   azure_client_id: "your-client-id-here"
   azure_client_secret: "your-client-secret-here"
   azure_redirect_uri: "https://your-homeassistant:8099/signin-oidc"
   osm_base_url: "https://www.onlinescoutmanager.co.uk"
   osm_api_token: "your-osm-token-here"
   ```
4. Click **Save**

### Step 4: Start Addon

1. Click **Start** button
2. Wait for addon to start (check **Log** tab for progress)
3. Enable **Start on boot** if desired
4. Enable **Show in sidebar** for easy access

### Step 5: Access Application

1. Click **Open Web UI** button, or
2. Navigate to `https://your-homeassistant:8099`
3. You'll be redirected to Microsoft login
4. Sign in with your Office 365 bookings inbox credentials
5. Grant consent to the application
6. You'll be redirected back to the dashboard

### Data Persistence

The addon stores data in `/data/` directory which is persisted:
- `/data/bookings.db` - SQLite database
- `/data/keys/` - Data protection keys for token encryption

This data persists across addon restarts and updates.

---

## Docker Standalone Deployment

For deployment outside of Home Assistant (e.g., standalone Docker host, VPS).

### Step 1: Clone Repository

```bash
git clone https://github.com/yourusername/bookings-assistant.git
cd bookings-assistant
```

### Step 2: Configure Environment Variables

Create `.env` file in project root:

```env
# Azure AD Configuration
AZURE_CLIENT_ID=your-client-id-here
AZURE_CLIENT_SECRET=your-client-secret-here
AZURE_REDIRECT_URI=http://localhost:5000/signin-oidc

# OSM Configuration
OSM_BASE_URL=https://www.onlinescoutmanager.co.uk
OSM_API_TOKEN=your-osm-token-here

# Database
DATABASE_PATH=/data/bookings.db

# ASP.NET Core
ASPNETCORE_URLS=http://+:5000
ASPNETCORE_ENVIRONMENT=Production
```

**Important:** Change `AZURE_REDIRECT_URI` to match your actual domain/IP.

### Step 3: Build Docker Image

```bash
docker build -t bookings-assistant:latest .
```

This multi-stage build will:
1. Build React frontend (`npm run build`)
2. Build .NET backend (`dotnet publish`)
3. Create final runtime image with both

Build time: ~5-10 minutes depending on hardware.

### Step 4: Run Container

```bash
docker run -d \
  --name bookings-assistant \
  -p 5000:5000 \
  -v $(pwd)/data:/data \
  --env-file .env \
  --restart unless-stopped \
  bookings-assistant:latest
```

**Explanation:**
- `-d` - Run in background
- `--name bookings-assistant` - Container name
- `-p 5000:5000` - Expose port 5000
- `-v $(pwd)/data:/data` - Mount data directory for database persistence
- `--env-file .env` - Load environment variables
- `--restart unless-stopped` - Auto-restart on failure

### Step 5: Verify Deployment

```bash
# Check container is running
docker ps | grep bookings-assistant

# Check logs
docker logs -f bookings-assistant

# Test endpoint
curl http://localhost:5000/api/health
```

You should see the application starting and listening on port 5000.

### Step 6: Access Application

1. Navigate to `http://localhost:5000` (or your server's IP/domain)
2. Sign in with Microsoft OAuth
3. Start using the application

### Production Considerations

**Reverse Proxy (Recommended):**

Use Nginx or Traefik as reverse proxy for HTTPS:

```nginx
# Nginx configuration example
server {
    listen 443 ssl http2;
    server_name bookings.yourdomain.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

**Docker Compose (Alternative):**

Create `docker-compose.yml`:

```yaml
version: '3.8'

services:
  bookings-assistant:
    build: .
    container_name: bookings-assistant
    ports:
      - "5000:5000"
    volumes:
      - ./data:/data
    env_file:
      - .env
    restart: unless-stopped
```

Run with:
```bash
docker-compose up -d
```

---

## Configuration Guide

### Application Settings

The application reads configuration from multiple sources (in order of precedence):

1. Environment variables (Docker/HA)
2. `appsettings.json` (default values)
3. `appsettings.Development.json` (development overrides)

### Key Configuration Options

**Authentication:**
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "common",
    "ClientId": "env:AZURE_CLIENT_ID",
    "ClientSecret": "env:AZURE_CLIENT_SECRET",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  }
}
```

**Database:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/data/bookings.db"
  }
}
```

**OSM Integration:**
```json
{
  "Osm": {
    "BaseUrl": "https://www.onlinescoutmanager.co.uk",
    "ApiToken": "env:OSM_API_TOKEN",
    "Timeout": 30
  }
}
```

**Logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### Environment Variables Reference

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `AZURE_CLIENT_ID` | Yes | - | Azure AD application ID |
| `AZURE_CLIENT_SECRET` | Yes | - | Azure AD client secret |
| `AZURE_REDIRECT_URI` | Yes | - | OAuth redirect URI |
| `OSM_BASE_URL` | Yes | - | OSM API base URL |
| `OSM_API_TOKEN` | Yes* | - | OSM API authentication token |
| `DATABASE_PATH` | No | `/data/bookings.db` | SQLite database file path |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` | ASP.NET Core environment |
| `ASPNETCORE_URLS` | No | `http://+:5000` | Listening URLs |

*Currently required but may be optional if using browser-based authentication flow.

---

## Troubleshooting

### Common Issues

#### 1. OAuth Redirect Error

**Problem:** After Microsoft login, you see "The reply URL specified in the request does not match the reply URLs configured for the application."

**Solution:**
- Verify `AZURE_REDIRECT_URI` environment variable matches exactly what's configured in Azure AD
- Check for trailing slashes (Azure AD is strict about this)
- Ensure protocol matches (http vs https)
- In Azure AD, go to **Authentication** → **Redirect URIs** and verify

#### 2. Database Permission Error

**Problem:** `SQLite Error: Unable to open database file`

**Solution:**
- Check `/data` directory has write permissions
- Docker: Ensure volume is mounted correctly (`-v $(pwd)/data:/data`)
- Home Assistant: Addon should have automatic access to `/data`
- Check disk space: `df -h /data`

#### 3. OSM API Authentication Failure

**Problem:** "Unauthorized" or "401" errors when fetching OSM data

**Solution:**
- Verify `OSM_API_TOKEN` is set correctly
- Token may have expired - re-extract from browser
- Check OSM service status: https://www.onlinescoutmanager.co.uk/status
- Review `docs/osm-api-discovery.md` for authentication method changes

#### 4. No Emails Showing on Dashboard

**Problem:** Dashboard shows 0 unread emails despite having unread emails in inbox

**Solution:**
- Check OAuth scopes include `Mail.Read`
- In Azure AD, verify permissions are granted (may need admin consent)
- Check if application is accessing correct mailbox
- Review logs: `docker logs bookings-assistant | grep Office365Service`
- **Current Status:** Office365Service is a stub - real implementation requires OAuth token flow

#### 5. Container Won't Start

**Problem:** Docker container exits immediately after starting

**Solution:**
```bash
# Check logs
docker logs bookings-assistant

# Common issues:
# - Missing environment variables (check .env file)
# - Port 5000 already in use (change port: -p 5001:5000)
# - Database migration failure (check logs for SQL errors)
```

#### 6. HTTPS Required Error in Home Assistant

**Problem:** OAuth won't work because Home Assistant requires HTTPS

**Solution:**
- Configure Home Assistant with SSL certificate
- Use Let's Encrypt via DuckDNS addon (recommended)
- Or use Nabu Casa Cloud for automatic HTTPS
- Update `AZURE_REDIRECT_URI` to use `https://`

#### 7. Slow Performance / Timeout

**Problem:** Dashboard takes long time to load or times out

**Solution:**
- Check network connectivity to Office 365 and OSM APIs
- Increase timeout values in configuration
- Check Home Assistant system resources: **Settings** → **System** → **Hardware**
- For large email inboxes, implement pagination (Phase 2)

### Debug Mode

Enable detailed logging for troubleshooting:

**Docker:**
```bash
docker run -e ASPNETCORE_ENVIRONMENT=Development ...
```

**Home Assistant:** Edit `config.yaml`:
```yaml
options:
  log_level: debug
```

Check logs:
```bash
# Docker
docker logs -f bookings-assistant

# Home Assistant
Settings → Add-ons → Bookings Assistant → Log tab
```

### Getting Help

If you encounter issues not covered here:

1. Check logs for specific error messages
2. Review GitHub Issues: https://github.com/yourusername/bookings-assistant/issues
3. Create new issue with:
   - Error message from logs
   - Deployment method (HA addon or Docker)
   - Environment details (HA version, Docker version)
   - Steps to reproduce

---

## Security Best Practices

1. **Protect Secrets:**
   - Never commit `.env` files or `appsettings.Production.json` to git
   - Use Azure Key Vault for production deployments
   - Rotate Azure AD client secrets periodically

2. **Network Security:**
   - Use HTTPS in production (required for OAuth)
   - Place behind reverse proxy with rate limiting
   - Consider firewall rules to limit API access

3. **Data Protection:**
   - Backup `/data/bookings.db` regularly
   - SQLite file contains cached email metadata and OAuth tokens
   - Tokens are encrypted at rest using ASP.NET Core Data Protection

4. **Updates:**
   - Keep Docker image updated with security patches
   - Monitor for ASP.NET Core and React dependency updates
   - Subscribe to GitHub releases for addon updates

---

## Next Steps

After successful deployment:

1. **First Run Configuration:**
   - Sign in with Microsoft OAuth
   - Application will create database and apply migrations
   - Click "Refresh" on dashboard to sync data

2. **Verify Integration:**
   - Check emails section shows unread emails (once OAuth is implemented)
   - Check bookings section shows OSM bookings (once API is configured)
   - Test linking an email to a booking

3. **Regular Operations:**
   - Click Refresh button to update data
   - Review linked emails and bookings
   - Add manual links as needed

4. **Read Development Guide:**
   - See `docs/development.md` for architecture details
   - Understand how to extend features for Phase 2

---

**Document Version:** 1.0.0
**Last Updated:** 2026-01-30
**Phase:** 1 MVP
