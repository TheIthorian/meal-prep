const runtimeEnv = import.meta.env;

export const config = {
    api: {
        baseUrl: runtimeEnv.VITE_API_BASE_URL || '',
    },
    app: {
        isDevMode: runtimeEnv.VITE_IS_DEV_MODE?.toLowerCase() === 'true',
    },
} as const;

export const appEnv = config.app;
