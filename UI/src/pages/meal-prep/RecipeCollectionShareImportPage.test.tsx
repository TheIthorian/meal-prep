// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const getShareLinkPreview = vi.fn();
const useAuth = vi.fn();

vi.mock('@/lib/api', () => ({
    recipeCollectionsApi: {
        getShareLinkPreview: (shareToken: string) => getShareLinkPreview(shareToken),
        importFromShareLink: vi.fn(),
    },
}));

vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => useAuth() }));
vi.mock('@/contexts/WorkspaceContext', () => ({
    useWorkspace: () => ({ workspaces: [], currentWorkspace: undefined }),
}));
vi.mock('@/lib/analytics', () => ({
    analyticsEvents: { shareLinkAuthPrompted: 'share_link_auth_prompted' },
    useAnalytics: () => ({ capture: vi.fn() }),
}));

const { default: RecipeCollectionShareImportPage } = await import('./RecipeCollectionShareImportPage');

const shareToken = 'abc123';

function renderPage() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    return render(
        <QueryClientProvider client={queryClient}>
            <MemoryRouter initialEntries={[`/share/recipe-collections/${shareToken}`]}>
                <Routes>
                    <Route path='/share/recipe-collections/:shareToken' element={<RecipeCollectionShareImportPage />} />
                </Routes>
            </MemoryRouter>
        </QueryClientProvider>,
    );
}

describe('RecipeCollectionShareImportPage', () => {
    afterEach(() => {
        cleanup();
    });

    beforeEach(() => {
        vi.clearAllMocks();
        getShareLinkPreview.mockResolvedValue({
            collectionName: 'Weeknight Dinners',
            description: 'Fast midweek meals',
            ownerWorkspaceName: 'Ben Kitchen',
            recipeCount: 2,
            recipeTitles: ['Sunday Roast', 'Tomato Soup'],
        });
    });

    it('renders the shared collection for a signed-out visitor', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        expect(await screen.findByText('Sunday Roast')).toBeDefined();
        expect(screen.getByText('Tomato Soup')).toBeDefined();
        expect(screen.getByText('Shared recipe collection')).toBeDefined();
    });

    it('invites a signed-out visitor to sign up or sign in and preserves the share link', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        const signUpLink = await screen.findByRole('link', { name: 'Create free account' });
        const signInLink = screen.getByRole('link', { name: 'Sign in' });

        const expectedReturnUrl = encodeURIComponent(`/share/recipe-collections/${shareToken}`);
        expect(signUpLink.getAttribute('href')).toBe(`/register?returnUrl=${expectedReturnUrl}`);
        expect(signInLink.getAttribute('href')).toBe(`/login?returnUrl=${expectedReturnUrl}`);
    });

    it('shows the import controls instead of the prompt once signed in', async () => {
        useAuth.mockReturnValue({ user: { userId: 'user-1' }, isLoading: false });

        renderPage();

        expect(await screen.findByRole('button', { name: 'Import collection' })).toBeDefined();
        expect(screen.queryByRole('link', { name: 'Create free account' })).toBeNull();
    });
});
