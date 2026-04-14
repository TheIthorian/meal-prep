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
        plugins: [react(), mode === 'development' && componentTagger()].filter(Boolean),
        resolve: {
            alias: {
                '@': path.resolve(__dirname, './src'),
            },
        },
    };
});
