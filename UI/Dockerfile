FROM node:22-alpine AS build

WORKDIR /app

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY UI/package.json UI/package.json

RUN corepack enable && pnpm install --frozen-lockfile

COPY UI UI

WORKDIR /app/UI

ARG VITE_API_BASE_URL=
ARG VITE_IS_DEV_MODE=false
ARG VITE_PUBLIC_POSTHOG_KEY=
ARG VITE_PUBLIC_POSTHOG_HOST=

ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
ENV VITE_IS_DEV_MODE=${VITE_IS_DEV_MODE}
ENV VITE_PUBLIC_POSTHOG_KEY=${VITE_PUBLIC_POSTHOG_KEY}
ENV VITE_PUBLIC_POSTHOG_HOST=${VITE_PUBLIC_POSTHOG_HOST}

RUN pnpm build

FROM caddy:2-alpine AS runtime

COPY --from=build /app/UI/dist /usr/share/caddy
COPY UI/Caddyfile /etc/caddy/Caddyfile
