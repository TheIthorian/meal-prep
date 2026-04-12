import { Toaster } from '@/components/ui/toaster';
import { Toaster as Sonner } from '@/components/ui/sonner';
import { TooltipProvider } from '@/components/ui/tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from '@/contexts/AuthContext';
import { WorkspaceProvider } from '@/contexts/WorkspaceContext';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { AppLayout } from '@/components/layouts/AppLayout';
import { WorkspaceRedirect } from '@/components/WorkspaceRedirect';
import Login from './pages/Login';
import Register from './pages/Register';
import Settings from './pages/Settings';
import TermsOfService from './pages/TermsOfService';
import DataRetention from './pages/DataRetention';
import NotFoundError from './pages/NotFoundError';
import ForbiddenError from './pages/ForbiddenError';
import Help from './pages/Help';
import { PostHogProvider } from '@posthog/react';
import { ThemeProvider } from '@/components/theme-provider';
import { StrictMode } from 'react';
import { AnalyticsBridge } from '@/components/AnalyticsBridge';

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            retry: 1,
            refetchOnWindowFocus: false,
        },
    },
});

const posthogOptions = { api_host: import.meta.env.VITE_PUBLIC_POSTHOG_HOST, defaults: '2026-01-30' } as const;

const App = () => (
    <StrictMode>
        <PostHogProvider apiKey={import.meta.env.VITE_PUBLIC_POSTHOG_KEY} options={posthogOptions}>
            <ThemeProvider defaultTheme='system' storageKey='vite-ui-theme'>
                <QueryClientProvider client={queryClient}>
                    <TooltipProvider>
                        <Toaster />
                        <Sonner />
                        <BrowserRouter>
                            <AuthProvider>
                                <WorkspaceProvider>
                                    <AnalyticsBridge />
                                    <Routes>
                                        <Route path='/' element={<WorkspaceRedirect />} />
                                        <Route path='/login' element={<Login />} />
                                        <Route path='/register' element={<Register />} />
                                        <Route path='/terms' element={<TermsOfService />} />
                                        <Route path='/data-retention' element={<DataRetention />} />
                                        <Route path='/help' element={<Help />} />
                                        <Route path='/403' element={<ForbiddenError />} />

                                        <Route
                                            path='/settings'
                                            element={
                                                <ProtectedRoute>
                                                    <AppLayout />
                                                </ProtectedRoute>
                                            }
                                        >
                                            <Route index element={<Settings />} />
                                        </Route>

                                        <Route
                                            path='/workspaces/:workspaceId'
                                            element={
                                                <ProtectedRoute>
                                                    <AppLayout />
                                                </ProtectedRoute>
                                            }
                                        >
                                            <Route path='settings' element={<Settings />} />
                                        </Route>

                                        <Route path='*' element={<NotFoundError />} />
                                    </Routes>
                                </WorkspaceProvider>
                            </AuthProvider>
                        </BrowserRouter>
                    </TooltipProvider>
                </QueryClientProvider>
            </ThemeProvider>
        </PostHogProvider>
    </StrictMode>
);

export default App;
