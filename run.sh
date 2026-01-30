#!/usr/bin/with-contenv bashio

CONFIG_PATH=/data/options.json

AZURE_CLIENT_ID=$(bashio::config 'azure_client_id')
AZURE_CLIENT_SECRET=$(bashio::config 'azure_client_secret')
AZURE_REDIRECT_URI=$(bashio::config 'azure_redirect_uri')
OSM_BASE_URL=$(bashio::config 'osm_base_url')
OSM_CAMPSITE_ID=$(bashio::config 'osm_campsite_id')
OSM_SECTION_ID=$(bashio::config 'osm_section_id')

export AzureAd__ClientId="${AZURE_CLIENT_ID}"
export AzureAd__ClientSecret="${AZURE_CLIENT_SECRET}"
export AzureAd__CallbackPath="${AZURE_REDIRECT_URI}"
export Osm__BaseUrl="${OSM_BASE_URL}"
export Osm__CampsiteId="${OSM_CAMPSITE_ID}"
export Osm__SectionId="${OSM_SECTION_ID}"

bashio::log.info "Starting Bookings Assistant..."
bashio::log.info "OSM Base URL: ${OSM_BASE_URL}"
bashio::log.info "OSM Campsite ID: ${OSM_CAMPSITE_ID}"
bashio::log.info "OSM Section ID: ${OSM_SECTION_ID}"

cd /app
exec dotnet BookingsAssistant.Api.dll
