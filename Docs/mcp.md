# MCP Server Usage and Deployment Caveats

Meal Prep exposes an MCP endpoint at `/mcp/{token}`.
Tokens are created in the UI under Integrations and are scoped to a single workspace.

## How To Use

1. In Meal Prep UI, go to Integrations and generate an MCP URL.
2. Copy the full URL and add it to your MCP client (Cursor, Claude, etc.).
3. Revoke tokens from Integrations when rotating or removing client access.

Important behavior:

- The token is embedded in the URL path.
- Anyone with that URL can access that workspace until the token is revoked.

## Deployment Caveats

### Reverse Proxy Routing Is Required

If you deploy behind Caddy/nginx/Traefik, ensure `/mcp/*` is forwarded to the API service.

The provided Caddy profiles already include this:

- `Infra/Caddyfile`
- `Infra/Caddyfile.http`

### Prefer HTTPS For MCP

Because the token is in the URL, HTTPS is strongly recommended:

- Prevents token exposure in transit.
- Reduces risk when using clients across devices/networks.

### Token Handling

- Do not paste MCP URLs in issue trackers or chat.
- Use one token per device/client for easy revocation.
- Revoke and recreate if a URL is leaked.

### Origin And Host Consistency

Generated URLs use the current request scheme + host. If your app is behind a proxy with a public host, ensure forwarded host/scheme are correct so generated URLs are usable by clients.

### HTTP-Only Deployments

`compose.http.yaml` supports MCP on LAN, but traffic is unencrypted. Treat this as convenience mode for trusted internal networks.
