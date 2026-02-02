# Database Migration Required

After implementing OSM OAuth support, a database migration needs to be created and applied.

## Steps to Complete

1. Stop the running application
2. Create the migration:
   ```bash
   cd BookingsAssistant.Api
   dotnet ef migrations add AddOsmOAuthSupport
   ```
3. Apply the migration:
   ```bash
   dotnet ef database update
   ```
4. Delete this file once migration is complete

## Changes in Migration

The migration renames `OsmApiToken` to `OsmAccessToken` and adds `OsmRefreshToken` field to the `ApplicationUsers` table.
