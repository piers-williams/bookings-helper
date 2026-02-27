# HA Addon Web View — Design

**Issue:** #4
**Date:** 2026-02-27
**Status:** Approved

## Problem

The HA addon exposes port 5000 internally (mapped to host port 8099) and the backend already serves a full React SPA via `UseStaticFiles` + `MapFallbackToFile`. However, the `bookings-assistant/config.yaml` has no `webui` field, so Home Assistant never shows the "Go to webview" button on the addon info page. Users must manually type the URL.

## Options Considered

### Option A — `webui` only (chosen)

Add a single line to `config.yaml`:

```yaml
webui: http://[HOST]:[PORT]/
```

HA substitutes `[HOST]` and `[PORT]` (using the first mapped port, 8099) at runtime and displays a "Go to webview" button on the addon info page. No code changes required.

- Risk: zero
- Files changed: 1 (`bookings-assistant/config.yaml`)

### Option B — Ingress only

Add `ingress: true`, `ingress_port: 5000`, `panel_icon`, `panel_title`. HA proxies requests through a dynamic base path (`/api/hassio_ingress/<token>/`) and shows the addon in the sidebar. Requires changes to `vite.config.ts` (`base: './'`), `main.tsx` (dynamic `BrowserRouter` basename from `X-Ingress-Path` header), and `Program.cs` (`UsePathBase`). Moderate complexity.

### Option C — `webui` + ingress

Both features together. Same complexity as Option B for the ingress side.

## Decision

**Option A.** The issue states sidebar support as "even better if" — a nice-to-have. The `webui` field directly satisfies the primary ask ("Go to webview" option) with zero risk. Ingress can be revisited as a separate issue if sidebar support is later desired.

## Implementation

Single change to `bookings-assistant/config.yaml` — add after the `ports_description` block:

```yaml
webui: http://[HOST]:[PORT]/
```

HA's `[HOST]` and `[PORT]` placeholders are resolved by the Supervisor using the first entry in the addon's `ports` map, which is `5000/tcp: 8099`, so the button will open `http://<ha-host>:8099/`.
