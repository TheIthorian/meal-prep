import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react-swc';
import path from 'path';
import { componentTagger } from 'lovable-tagger';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => ({
    server: {
        host: '::',
        port: 8080,
        // Same-origin /api in dev — avoids cross-port CORS and HTTPS redirects to the API.
        proxy: {
            '/api': {
                target: apiBaseUrl,
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
}));
