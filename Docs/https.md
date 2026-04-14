# HTTPS Notes

This is the operational checklist for setting up HTTPS deployments on self-hosted Docker environments.

## 1) Certificate Files

Place certificate and private key in:

- `Infra/certs/cert.pem`
- `Infra/certs/key.pem`

The HTTPS compose profile mounts this directory into Caddy.

## 2) Hostname/DNS

Make sure the hostname in your certificate resolves to your deployment host.

Typical LAN setup:

- DNS or hosts entry: `mealprep.local` -> `<host-ip>`

If you access by IP over HTTPS, the cert must include that IP in SAN entries.

## 3) Start HTTPS Profile

```bash
docker compose -f compose.https.yaml up -d --build
```

## 4) CORS and App Env

In `.docker.env`, set `CORS_ORIGINS` to include the HTTPS origin, for example:

```env
CORS_ORIGINS=https://mealprep.local
```

## 5) Verify

```bash
curl -kI https://mealprep.local
curl -kI https://mealprep.local/api/health
```

Expected:

- UI returns `200`
- API `/api/health` returns `200` when unauthenticated

## Gotchas

- Forgetting to mount cert files causes Caddy TLS startup failure.
- Wrong hostname/cert pairing causes browser certificate warnings.
- Missing HTTPS origin in `CORS_ORIGINS` breaks UI->API requests.
