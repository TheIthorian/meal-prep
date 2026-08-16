import { buildProxyRequest } from './proxy.js';

type Env = {
    API_ORIGIN: string;
};

/**
 * Same-origin proxy for the Meal Prep API. Everything under /api is forwarded to
 * API_ORIGIN so the browser and the API share an origin.
 *
 * This is what makes the deployed app work at all: auth is an ASP.NET Identity cookie
 * with SameSite=Lax, which a browser will not send from pages.dev to railway.app. Behind
 * this proxy the cookie is first-party and the vite dev proxy's behaviour is reproduced
 * in production.
 */
export const onRequest: PagesFunction<Env> = async ({ request, env }) => {
    if (!env.API_ORIGIN) {
        return new Response('API_ORIGIN is not configured for this Pages project', { status: 500 });
    }

    return fetch(buildProxyRequest({ request, apiOrigin: env.API_ORIGIN }));
};
