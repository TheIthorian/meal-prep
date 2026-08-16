import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react-swc';
import path from 'path';
import { componentTagger } from 'lovable-tagger';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), '');
    const apiProxyTarget = env.VITE_API_PROXY_TARGET || env.VITE_API_BASE_URL || 'http://127.0.0.1:5001';

    return {
        server: {
            host: '::',
            port: 8080,
            // Same-origin /api in dev — avoids cross-port CORS and HTTPS redirects to the API.
            proxy: {
                '/api': {
                    target: apiProxyTarget,
                    changeOrigin: true,
                },
            },
        },
        build: {
            // Vite's default ('modules') still targets Safari 14, which costs downlevelling and
            // shipped polyfills for syntax every browser we support has had for years — Lighthouse
            // flagged 8 KiB of them. es2022 is baseline-available across current browsers.
            target: 'es2022',

            // Emitted alongside the bundle but not referenced by it, so stack traces from
            // production can be symbolicated without serving maps to visitors. Lighthouse's
            // valid-source-maps audit was failing outright.
            sourcemap: 'hidden',

            rollupOptions: {
                output: {
                    // Dependencies change far less often than app code, so keeping them in their
                    // own chunks means a UI deploy doesn't invalidate them in the browser cache.
                    manualChunks: {
                        react: ['react', 'react-dom', 'react-router-dom'],
                        query: ['@tanstack/react-query'],
                        motion: ['framer-motion'],
                        analytics: ['posthog-js', '@posthog/react'],
                        http: ['axios'],
                    },
                },
            },
        },
        plugins: [react(), mode === 'development' && componentTagger()].filter(Boolean),
        resolve: {
            alias: {
                '@': path.resolve(__dirname, './src'),
            },
        },
    };
});
