#!/bin/bash
set -e

# Read HA addon options from /data/options.json
if [ -f /data/options.json ]; then
    export Osm__BaseUrl=$(jq -r '.osm_base_url // "https://www.onlinescoutmanager.co.uk"' /data/options.json)
    export Osm__CampsiteId=$(jq -r '.osm_campsite_id // "219"' /data/options.json)
    export Osm__SectionId=$(jq -r '.osm_section_id // "56710"' /data/options.json)
    export Osm__ClientId=$(jq -r '.osm_client_id // ""' /data/options.json)
    export Osm__ClientSecret=$(jq -r '.osm_client_secret // ""' /data/options.json)
    export GateCode__DaysBefore=$(jq -r '.gate_code_days_before // "2"' /data/options.json)
    export GateCode__CampaignId=$(jq -r '.gate_code_campaign_id // "123054"' /data/options.json)
    export GateCode__FromName=$(jq -r '.gate_code_from_name // ""' /data/options.json)
    export GateCode__FromEmail=$(jq -r '.gate_code_from_email // ""' /data/options.json)
    export GateCode__Subject=$(jq -r '.gate_code_subject // "Gate code"' /data/options.json)
    export Auth__ApiToken=$(jq -r '.api_token // ""' /data/options.json)
    export OpenWebUi__BaseUrl=$(jq -r '.open_webui_base_url // ""' /data/options.json)
    export OpenWebUi__ApiKey=$(jq -r '.open_webui_api_key // ""' /data/options.json)
    export OpenWebUi__Model=$(jq -r '.open_webui_model // ""' /data/options.json)
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
