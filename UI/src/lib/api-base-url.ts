import { config } from '@/lib/env';

function getDefaultApiBaseUrl() {
    return '';
}

export function getApiBaseUrl() {
    return config.api.baseUrl || getDefaultApiBaseUrl();
}
