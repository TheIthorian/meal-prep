import { Toaster } from '@/components/ui/toaster';
import { Toaster as Sonner } from '@/components/ui/sonner';
import { TooltipProvider } from '@/components/ui/tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from '@/contexts/AuthContext';
import { WorkspaceProvider } from '@/contexts/WorkspaceContext';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { WorkspaceRedirect } from '@/components/WorkspaceRedirect';
import { MealPrepAppLayout } from '@/components/meal-prep/MealPrepAppLayout';
import Login from './pages/Login';
import Register from './pages/Register';
import Settings from './pages/Settings';
import SettingsRedirectPage from './pages/SettingsRedirectPage';
import RecipeLibraryPage from './pages/meal-prep/RecipeLibraryPage';
import RecipeDetailPage from './pages/meal-prep/RecipeDetailPage';
import RecipeCollectionsListPage from './pages/meal-prep/RecipeCollectionsListPage';
import RecipeCollectionPage from './pages/meal-prep/RecipeCollectionPage';
import RecipeCollectionShareImportPage from './pages/meal-prep/RecipeCollectionShareImportPage';
import WeeklyPlannerPage from './pages/meal-prep/WeeklyPlannerPage';
import ShoppingListPage from './pages/meal-prep/ShoppingListPage';
import ShoppingModePage from './pages/meal-prep/ShoppingModePage';
import CookingModePage from './pages/meal-prep/CookingModePage';
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
                                                    <SettingsRedirectPage />
                                                </ProtectedRoute>
                                            }
                                        />

                                        <Route
                                            path='/workspaces/:workspaceId'
                                            element={
                                                <ProtectedRoute>
                                                    <MealPrepAppLayout />
                                                </ProtectedRoute>
                                            }
                                        >
                                            <Route index element={<RecipeLibraryPage />} />
                                            <Route path='collections' element={<RecipeCollectionsListPage />} />
                                            <Route path='collections/:collectionId' element={<RecipeCollectionPage />} />
                                            <Route path='recipe/:recipeId' element={<RecipeDetailPage />} />
                                            <Route path='next-meals' element={<WeeklyPlannerPage />} />
                                            <Route path='shopping' element={<ShoppingListPage />} />
                                            <Route path='settings' element={<Settings />} />
                                        </Route>

                                        <Route
                                            path='/workspaces/:workspaceId/shopping-mode'
                                            element={
                                                <ProtectedRoute>
                                                    <ShoppingModePage />
                                                </ProtectedRoute>
                                            }
                                        />
                                        <Route
                                            path='/share/recipe-collections/:shareToken'
                                            element={
                                                <ProtectedRoute>
                                                    <RecipeCollectionShareImportPage />
                                                </ProtectedRoute>
                                            }
                                        />
                                        <Route
                                            path='/workspaces/:workspaceId/cooking/:recipeId'
                                            element={
                                                <ProtectedRoute>
                                                    <CookingModePage />
                                                </ProtectedRoute>
                                            }
                                        />

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
