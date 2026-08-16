# Deployment

The API runs on Railway, the UI on Cloudflare Pages.

## Why the UI proxies the API

Auth is an ASP.NET Identity cookie with `SameSite=Lax`. A browser will not attach that cookie to a
request from `meal-prep-ui.pages.dev` to `*.up.railway.app`, so a build that calls the Railway URL
directly logs in successfully and then 401s on every subsequent request.

`UI/functions/api/[[path]].ts` is a Cloudflare Pages Function that forwards everything under `/api`
to `API_ORIGIN`. The browser only ever talks to the Pages origin, the cookie stays first-party, and
production behaves like the vite dev proxy. Consequently `VITE_API_BASE_URL` must stay unset for
production builds — setting it re-introduces the cross-origin problem.

## Railway (API)

`railway.json` configures the build (Dockerfile at `Api/Dockerfile`) and a healthcheck against
`/api/health`. Deploys happen through Railway's GitHub integration on push to `main`; there is no
deploy workflow to run.

`Infra/railway.resources.yml` records the services the project expects (Postgres, Redis, a bucket)
and the non-secret variables. Set these in the Railway dashboard as secrets:

| Variable | Source |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Postgres service |
| `ConnectionStrings__Redis` | Redis service |
| `S3__ServiceUrl`, `S3__AccessKey`, `S3__SecretKey`, `S3__BucketName` | Bucket |
| `OpenAI__ApiKey` | OpenRouter |

`ASPNETCORE_HTTP_PORTS` must be `${{PORT}}` — Railway chooses the port and the container has to
listen on that one.

## Cloudflare Pages (UI)

`.github/workflows/frontend-deploy.yml` builds `meal-prep-ui` and deploys it on push to `main`.
Required repo secrets: `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `VITE_PUBLIC_POSTHOG_KEY`.

`UI/wrangler.toml` sets `pages_build_output_dir` and the `API_ORIGIN` var. Point `API_ORIGIN` at
the Railway service URL — it is read at request time by the function, not baked into the bundle, so
changing it does not require a rebuild. The deploy command deliberately passes no output directory:
deploying via the config file is what makes wrangler pick up `UI/functions`.

## Local dev data

Postgres, Redis and MinIO bind-mount a directory on the host rather than using named volumes, so
neither `docker compose down -v` nor anything that cleans the working tree can wipe the recipe
library. The default location is `~/Documents/meal-prep-data`, deliberately outside the repo;
override it with `MEAL_PREP_DATA_DIR`:

```bash
MEAL_PREP_DATA_DIR=/Volumes/external/meal-prep docker compose up -d
```

Migrating an existing setup off the old named volumes:

```bash
# Postgres must not be running while its data directory is copied.
docker compose stop db redis minio

DATA_DIR=~/Documents/meal-prep-data
mkdir -p "$DATA_DIR"/postgres "$DATA_DIR"/minio "$DATA_DIR"/redis

# Root in a throwaway container, so ownership inside the data directory (postgres runs as
# uid 999) survives the copy.
docker run --rm -v meal-prep_postgres-data:/from:ro -v "$DATA_DIR/postgres":/to \
    alpine sh -c 'cd /from && cp -a . /to/'
docker run --rm -v meal-prep_miniodata:/from:ro -v "$DATA_DIR/minio":/to \
    alpine sh -c 'cd /from && cp -a . /to/'
docker run --rm -v meal-prep_redis:/from:ro -v "$DATA_DIR/redis":/to \
    alpine sh -c 'cd /from && cp -a . /to/'

docker compose up -d db redis minio
```

Each target directory must be empty first — Postgres will not initialise into a directory that
already holds a cluster, and the copy would land beside it.

This only reads from the volumes, so the old copies remain as a rollback. Remove them once the
recipe count and images look right:

```bash
docker volume rm meal-prep_postgres-data meal-prep_miniodata meal-prep_redis
```
