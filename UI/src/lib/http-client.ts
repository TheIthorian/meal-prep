import axios, { AxiosInstance, AxiosError, AxiosRequestConfig, Axios } from 'axios';
import { toast } from '@/hooks/use-toast';
import { ToastActionElement } from '@/components/ui/toast';

/**
 * Detailed validation error
 */
interface ProblemDetail {
    type?: string;
    title?: string;
    status?: number;
    detail?: string;
    instance?: string;
    errors?: Record<string, string[]>;
}

/**
 * Network & stack errors
 */
interface PlatformError {
    error: string;
}

/**
 * Simple validation / logic errors
 */
interface AppError {
    type: string;
    title: string;
    detail?: string;
    status: number;
}

class AppHttpClient {
    private axiosInstance: AxiosInstance;

    constructor() {
        this.axiosInstance = axios.create({
            // In dev, default to same-origin + Vite proxy (see vite.config.ts). Override with VITE_API_BASE_URL.
            baseURL:
                import.meta.env.VITE_API_BASE_URL ||
                (import.meta.env.DEV ? '' : 'http://localhost:5001'),
            withCredentials: true,
            headers: { 'Content-Type': 'application/json' },
        });

        this.setupInterceptors();
    }

    private setupInterceptors() {
        this.axiosInstance.interceptors.request.use(config => {
            if (config.data instanceof FormData && config.headers) {
                const headers = config.headers;
                if (typeof headers.delete === 'function') {
                    headers.delete('Content-Type');
                } else {
                    delete (headers as Record<string, unknown>)['Content-Type'];
                }
            }
            return config;
        });

        // Response interceptor for error handling
        this.axiosInstance.interceptors.response.use(
            response => response,
            (error: AxiosError<ProblemDetail> | AxiosError<PlatformError>) => {
                // Handle 401 by redirecting to login (avoid loop if already on login or other public pages)
                if (error.response?.status === 401) {
                    this.handleUnauthorizedError(error);
                    return;
                }

                handleAxiosError(error);
                return Promise.reject(error);
            },
        );
    }

    private handleUnauthorizedError(error: AxiosError<ProblemDetail> | AxiosError<PlatformError>) {
        const pathname = window.location.pathname;
        const publicPaths = ['/login', '/register', '/terms', '/data-retention', '/help'];

        if (!publicPaths.some(path => pathname.startsWith(path))) {
            window.location.href = '/login';
            return;
        }

        if (isAppException(error) && error.response.data.title === 'Unauthorized') {
            // Account locked
            if (error.response.data.detail === 'LockedOut') {
                toast({
                    title: 'Too many sign-in attempts',
                    description: 'Your account is temporarily locked. Try again later.',
                    variant: 'destructive',
                });
            } else {
                toast({
                    title: 'Unable to sign in',
                    description: 'The email or password you entered is incorrect. Please try again.',
                    variant: 'destructive',
                });
            }
        }
    }

    async get<T>(url: string, config?: AxiosRequestConfig) {
        const response = await this.axiosInstance.get<T>(url, config);
        return response.data;
    }

    async post<T>(url: string, data?: unknown, config?: AxiosRequestConfig) {
        const response = await this.axiosInstance.post<T>(url, data, config);
        return response.data;
    }

    async postFormData<T>(url: string, data: FormData) {
        const response = await this.axiosInstance.post<T>(url, data);
        return response.data;
    }

    async put<T>(url: string, data?: unknown, config?: AxiosRequestConfig) {
        const response = await this.axiosInstance.put<T>(url, data, config);
        return response.data;
    }

    async patch<T>(url: string, data?: unknown, config?: AxiosRequestConfig) {
        const response = await this.axiosInstance.patch<T>(url, data, config);
        return response.data;
    }

    async delete<T>(url: string, config?: AxiosRequestConfig) {
        const response = await this.axiosInstance.delete<T>(url, config);
        return response.data;
    }
}

export function handleAxiosError(error: AxiosError<unknown>) {
    if (isProblemError(error)) {
        const problemDetail = error.response.data;

        // Handle validation errors
        if (problemDetail.errors) {
            const errorMessages = Object.entries(problemDetail.errors)
                .map(([_field, messages]) => `${messages.join(', ')}`)
                .join('\n');

            toast({
                title: problemDetail.title || 'Validation Error',
                description: errorMessages,
                variant: 'destructive',
            });
        } else {
            // Handle general errors
            toast({
                title: problemDetail.title || 'Error',
                description: problemDetail.detail || 'An unexpected error occurred. Please try again later.',
                variant: 'destructive',
            });
        }
    } else if (
        error.response?.status &&
        [502, 503, 504].includes(error.response.status)
    ) {
        // Network error
        toast({
            title: 'Network Error',
            description: 'Unable to connect to the server. Please try again later.',
            variant: 'destructive',
        });
    } else if (isAppException(error)) {
        const errorData = error.response.data;
        toast({ title: errorData.title, description: errorData.detail, variant: 'destructive' });
    } else if (isPlatformError(error)) {
        toast({
            title: error.response?.data?.error || error.message || 'Error',
            variant: 'destructive',
        });
    } else {
        // Other errors (e.g. CORS/network — often no response object)
        const causeMsg =
            error.cause && typeof error.cause === 'object' && 'message' in error.cause
                ? String((error.cause as { message?: unknown }).message)
                : undefined;
        toast({
            title: error.message || 'Error',
            description:
                causeMsg || error.message || 'An unexpected error occurred. Please try again later.',
            variant: 'destructive',
        });
    }
}

function isProblemError(
    error: AxiosError<ProblemDetail> | AxiosError<PlatformError>,
): error is AxiosError<ProblemDetail> {
    return error.response?.data !== undefined && 'errors' in error.response.data;
}

function isAppException(error: unknown): error is AxiosError<AppError> {
    return (
        error instanceof AxiosError &&
        'response' in error &&
        'title' in error.response.data &&
        'type' in error.response.data
    );
}

function isPlatformError(error: unknown): error is AxiosError<PlatformError> {
    return error instanceof AxiosError && 'response' in error && 'error' in error.response.data;
}

export const httpClient = new AppHttpClient();
