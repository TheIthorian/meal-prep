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
import { PostHogProvider } from '@posthog/react';
import { ThemeProvider } from '@/components/theme-provider';
import { StrictMode, Suspense, lazy, type ReactNode } from 'react';
import { AnalyticsBridge } from '@/components/AnalyticsBridge';
import { FullPageSpinner } from '@/components/FullPageSpinner';

// The recipe library is the landing route for a signed-in user, so it stays a static import:
// splitting it would only add a second round trip in front of the page people actually see.
import RecipeLibraryPage from './pages/meal-prep/RecipeLibraryPage';

// Every other page loads on demand. Statically importing all of them put the whole app —
// planner, shopping mode, cooking mode, settings, the legal pages — into the entry chunk, of
// which Lighthouse measured 210 KiB unused on first load.
const Login = lazy(() => import('./pages/Login'));
const Register = lazy(() => import('./pages/Register'));
const Settings = lazy(() => import('./pages/Settings'));
const SettingsRedirectPage = lazy(() => import('./pages/SettingsRedirectPage'));
const RecipeDetailPage = lazy(() => import('./pages/meal-prep/RecipeDetailPage'));
const RecipeCollectionsListPage = lazy(() => import('./pages/meal-prep/RecipeCollectionsListPage'));
const RecipeCollectionPage = lazy(() => import('./pages/meal-prep/RecipeCollectionPage'));
const RecipeCollectionShareImportPage = lazy(() => import('./pages/meal-prep/RecipeCollectionShareImportPage'));
const SharedRecipeDetailPage = lazy(() => import('./pages/meal-prep/SharedRecipeDetailPage'));
const SharedRecipePage = lazy(() => import('./pages/meal-prep/SharedRecipePage'));
const WeeklyPlannerPage = lazy(() => import('./pages/meal-prep/WeeklyPlannerPage'));
const ShoppingListPage = lazy(() => import('./pages/meal-prep/ShoppingListPage'));
const ShoppingModePage = lazy(() => import('./pages/meal-prep/ShoppingModePage'));
const CookingModePage = lazy(() => import('./pages/meal-prep/CookingModePage'));
const TermsOfService = lazy(() => import('./pages/TermsOfService'));
const DataRetention = lazy(() => import('./pages/DataRetention'));
const NotFoundError = lazy(() => import('./pages/NotFoundError'));
const ForbiddenError = lazy(() => import('./pages/ForbiddenError'));
const Help = lazy(() => import('./pages/Help'));

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            retry: 1,
            refetchOnWindowFocus: false,
        },
    },
});

const posthogOptions = { api_host: import.meta.env.VITE_PUBLIC_POSTHOG_HOST, defaults: '2026-01-30' } as const;

const posthogKey = import.meta.env.VITE_PUBLIC_POSTHOG_KEY;

/**
 * Mounts the PostHog provider only when a key is configured.
 *
 * Rendering the provider without one makes posthog-js log an error on every page load
 * ("PostHog was initialized without a token"). Builds with analytics turned off — local dev, and
 * any deploy without the key set — should simply not have analytics rather than announce a
 * misconfiguration. `usePostHog` falls back to an uninitialised client outside a provider and
 * `useAnalytics` already null-checks it, so callers keep working either way.
 */
const AnalyticsProvider = ({ children }: { children: ReactNode }) =>
    posthogKey ? (
        <PostHogProvider apiKey={posthogKey} options={posthogOptions}>
            {children}
        </PostHogProvider>
    ) : (
        <>{children}</>
    );

const App = () => (
    <StrictMode>
        <AnalyticsProvider>
            <ThemeProvider defaultTheme='system' storageKey='vite-ui-theme'>
                <QueryClientProvider client={queryClient}>
                    <TooltipProvider>
                        <Toaster />
                        <Sonner />
                        <BrowserRouter>
                            <AuthProvider>
                                <WorkspaceProvider>
                                    <AnalyticsBridge />
                                    <Suspense fallback={<FullPageSpinner />}>
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
                                                <Route
                                                    path='collections/:collectionId'
                                                    element={<RecipeCollectionPage />}
                                                />
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
                                            {/* Public: signed-out visitors see the shared collection and a prompt to join. */}
                                            <Route
                                                path='/share/recipe-collections/:shareToken'
                                                element={<RecipeCollectionShareImportPage />}
                                            />
                                            <Route
                                                path='/share/recipe-collections/:shareToken/recipes/:recipeId'
                                                element={<SharedRecipeDetailPage />}
                                            />
                                            <Route path='/share/recipes/:shareToken' element={<SharedRecipePage />} />
                                            <Route
                                                path='/workspaces/:workspaceId/cooking/:recipeId'
                                                element={
                                                    <ProtectedRoute>
                                                        <CookingModePage />
                                                    </ProtectedRoute>
                                                }
                                            />
                                            {/* Cooking mode from a share link needs an account, so a signed-out
                                                visitor is sent to sign in and returned here afterwards. */}
                                            <Route
                                                path='/share/recipes/:shareToken/cooking'
                                                element={
                                                    <ProtectedRoute>
                                                        <CookingModePage />
                                                    </ProtectedRoute>
                                                }
                                            />
                                            <Route
                                                path='/share/recipe-collections/:shareToken/recipes/:recipeId/cooking'
                                                element={
                                                    <ProtectedRoute>
                                                        <CookingModePage />
                                                    </ProtectedRoute>
                                                }
                                            />

                                            <Route path='*' element={<NotFoundError />} />
                                        </Routes>
                                    </Suspense>
                                </WorkspaceProvider>
                            </AuthProvider>
                        </BrowserRouter>
                    </TooltipProvider>
                </QueryClientProvider>
            </ThemeProvider>
        </AnalyticsProvider>
    </StrictMode>
);

export default App;
