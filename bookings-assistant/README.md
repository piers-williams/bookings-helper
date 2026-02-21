# Bookings Assistant - Home Assistant Addon

Manage scout campsite bookings from Online Scout Manager (OSM) and Office 365 Calendar.

## Features

- Fetch bookings from Online Scout Manager campsite module
- Sync bookings to Office 365 Calendar with Microsoft Graph API
- Automatic synchronization on schedule
- Web interface for manual sync and monitoring
- Persistent storage for SQLite database and credentials

## Installation

### 1. Add Repository to Home Assistant

1. Navigate to **Settings** > **Add-ons** > **Add-on Store**
2. Click the three dots menu (top right) and select **Repositories**
3. Add this repository URL: `https://github.com/piers-williams/bookings-helper`
4. Click **Add** and then **Close**

### 2. Install the Addon

1. Find **Bookings Assistant** in your addon store
2. Click on it and press **Install**
3. Wait for the installation to complete

## Configuration

Before starting the addon, you must configure the following options:

### Azure AD Configuration

To sync bookings to Office 365 Calendar, you need to register an application in Azure AD:

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Configure:
   - **Name**: Bookings Assistant
   - **Supported account types**: Accounts in this organizational directory only
   - **Redirect URI**: `http://your-home-assistant:8099/signin-oidc`
5. Click **Register**
6. Note the **Application (client) ID** - this is your `azure_client_id`
7. Go to **Certificates & secrets** > **New client secret**
8. Create a secret and note the **Value** - this is your `azure_client_secret`
9. Go to **API permissions** and add:
   - Microsoft Graph > Delegated > `Calendars.ReadWrite`
   - Microsoft Graph > Delegated > `User.Read`
10. Grant admin consent if required

### Online Scout Manager Configuration

Default values are provided for OSM configuration:

- **osm_base_url**: `https://www.onlinescoutmanager.co.uk` (default)
- **osm_campsite_id**: `219` (default campsite)
- **osm_section_id**: `56710` (default section)

You can override these if you need to use different values for your scout group.

### Addon Configuration

Edit the addon configuration with your values:

```yaml
azure_client_id: "your-application-client-id"
azure_client_secret: "your-client-secret-value"
azure_redirect_uri: "http://your-home-assistant:8099/signin-oidc"
osm_base_url: "https://www.onlinescoutmanager.co.uk"
osm_campsite_id: "219"
osm_section_id: "56710"
```

**Configuration Options:**

- **azure_client_id** (required): Azure AD Application (client) ID
- **azure_client_secret** (required): Azure AD client secret
- **azure_redirect_uri** (required): OAuth redirect URI (must match Azure AD registration)
- **osm_base_url** (optional): OSM API base URL (default: https://www.onlinescoutmanager.co.uk)
- **osm_campsite_id** (optional): OSM campsite ID (default: 219)
- **osm_section_id** (optional): OSM section ID (default: 56710)

## Port Mapping

The addon exposes the web interface on port **8099**.

- **Internal Port**: 5000 (ASP.NET application)
- **Host Port**: 8099 (Home Assistant)

Access the web interface at: `http://your-home-assistant:8099`

## Volume Mapping

The addon maps a persistent data directory:

- **Host**: `/data` (Home Assistant addon data directory)
- **Container**: `/data` (application data)

This directory stores:

- SQLite database (`bookings.db`)
- User credentials and tokens
- Application logs

Data persists across addon restarts and updates.

## First Run

1. Save your configuration
2. Start the addon
3. Check the logs for startup messages
4. Navigate to `http://your-home-assistant:8099` in your browser
5. You'll be redirected to Microsoft login to authorize calendar access
6. After authorization, configure your OSM credentials in the web interface
7. Trigger your first sync to test the integration

## Usage

### Web Interface

Access the web interface to:

- View current bookings
- Manually trigger synchronization
- Monitor sync status and logs
- Configure OSM credentials (API key and secret)

### Automatic Synchronization

The addon automatically syncs bookings on a schedule (configurable in the application).

## Troubleshooting

### Logs

View addon logs in Home Assistant:

1. Go to **Settings** > **Add-ons** > **Bookings Assistant**
2. Click the **Log** tab

### Common Issues

**Authentication Errors**

- Verify Azure AD client ID and secret are correct
- Check redirect URI matches Azure AD registration exactly
- Ensure required API permissions are granted in Azure AD

**OSM Connection Errors**

- Verify OSM base URL is correct
- Check OSM campsite ID and section ID
- Ensure OSM API credentials are configured in web interface

**Port Already in Use**

- Check if port 8099 is available on your Home Assistant host
- Change the port mapping in Home Assistant addon configuration if needed

**Data Persistence Issues**

- Verify `/data` volume is mounted correctly
- Check Home Assistant addon data directory permissions

## Security Model

This addon is designed to run exclusively on a private Home Assistant network. The API endpoints have **no authentication** — any device on the same network can read bookings data and trigger syncs. Do not expose port 8099 to the internet.

## Support

For issues and feature requests, please visit:
https://github.com/piers-williams/bookings-helper/issues

## License

See LICENSE file in the repository.
