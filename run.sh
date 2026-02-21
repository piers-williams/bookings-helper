#!/usr/bin/with-contenv bashio

CONFIG_PATH=/data/options.json

OSM_BASE_URL=$(bashio::config 'osm_base_url')
OSM_CAMPSITE_ID=$(bashio::config 'osm_campsite_id')
OSM_SECTION_ID=$(bashio::config 'osm_section_id')
OSM_CLIENT_ID=$(bashio::config 'osm_client_id')
OSM_CLIENT_SECRET=$(bashio::config 'osm_client_secret')

export Osm__BaseUrl="${OSM_BASE_URL}"
export Osm__CampsiteId="${OSM_CAMPSITE_ID}"
export Osm__SectionId="${OSM_SECTION_ID}"
export Osm__ClientId="${OSM_CLIENT_ID}"
export Osm__ClientSecret="${OSM_CLIENT_SECRET}"

bashio::log.info "Starting Bookings Assistant..."
bashio::log.info "OSM Base URL: ${OSM_BASE_URL}"
bashio::log.info "OSM Campsite ID: ${OSM_CAMPSITE_ID}"
bashio::log.info "OSM Section ID: ${OSM_SECTION_ID}"

cd /app
exec dotnet BookingsAssistant.Api.dll
