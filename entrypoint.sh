#!/bin/bash
set -e

# Read HA addon options from /data/options.json
if [ -f /data/options.json ]; then
    export Osm__BaseUrl=$(jq -r '.osm_base_url // "https://www.onlinescoutmanager.co.uk"' /data/options.json)
    export Osm__CampsiteId=$(jq -r '.osm_campsite_id // "219"' /data/options.json)
    export Osm__SectionId=$(jq -r '.osm_section_id // "56710"' /data/options.json)
    export Osm__ClientId=$(jq -r '.osm_client_id // ""' /data/options.json)
    export Osm__ClientSecret=$(jq -r '.osm_client_secret // ""' /data/options.json)
    echo "Options loaded from /data/options.json"
else
    echo "No /data/options.json found, using defaults"
fi

# Use HTTPS if HA SSL certs are available, otherwise fall back to HTTP
if [ -f /ssl/fullchain.pem ] && [ -f /ssl/privkey.pem ]; then
    export ASPNETCORE_URLS="https://+:5000"
    export Kestrel__Certificates__Default__Path="/ssl/fullchain.pem"
    export Kestrel__Certificates__Default__KeyPath="/ssl/privkey.pem"
    echo "HTTPS enabled using /ssl/fullchain.pem"
else
    export ASPNETCORE_URLS="http://+:5000"
    echo "SSL certs not found, falling back to HTTP"
fi

exec dotnet BookingsAssistant.Api.dll
