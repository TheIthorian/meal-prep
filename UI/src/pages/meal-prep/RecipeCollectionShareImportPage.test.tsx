// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { TooltipProvider } from '@/components/ui/tooltip';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const getShareLinkPreview = vi.fn();
const useAuth = vi.fn();

vi.mock('@/lib/api', () => ({
    recipeCollectionsApi: {
        getShareLinkPreview: (shareToken: string) => getShareLinkPreview(shareToken),
        importFromShareLink: vi.fn(),
        sharedRecipeImageUrl: (token: string, recipeId: string, width?: number) =>
            `/api/v1/recipe-collection-share/${token}/recipes/${recipeId}/image${width ? `?w=${width}` : ''}`,
    },
}));

vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => useAuth() }));
const workspace = { workspaceId: 'workspace-1', name: 'Home' };
vi.mock('@/contexts/WorkspaceContext', () => ({
    useWorkspace: () => ({ workspaces: [workspace], currentWorkspace: workspace }),
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
            <TooltipProvider>
                <MemoryRouter initialEntries={[`/share/recipe-collections/${shareToken}`]}>
                    <Routes>
                        <Route
                            path='/share/recipe-collections/:shareToken'
                            element={<RecipeCollectionShareImportPage />}
                        />
                    </Routes>
                </MemoryRouter>
            </TooltipProvider>
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
            recipes: [
                {
                    id: 'recipe-1',
                    title: 'Sunday Roast',
                    description: 'A slow roasted centrepiece',
                    servings: 4,
                    prepMinutes: 25,
                    cookMinutes: 90,
                    tags: ['dinner'],
                    hasImage: true,
                },
                {
                    id: 'recipe-2',
                    title: 'Tomato Soup',
                    description: null,
                    servings: 2,
                    prepMinutes: 10,
                    cookMinutes: 20,
                    tags: [],
                    hasImage: false,
                },
            ],
        });
    });

    it('renders the shared collection for a signed-out visitor', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        expect(await screen.findByText('Sunday Roast')).toBeDefined();
        expect(screen.getByText('Tomato Soup')).toBeDefined();
        expect(screen.getByText('Shared recipe collection')).toBeDefined();
    });

    it('links each recipe card to its shared detail page', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        const roastLink = await screen.findByRole('link', { name: /Sunday Roast/ });

        expect(roastLink.getAttribute('href')).toBe(`/share/recipe-collections/${shareToken}/recipes/recipe-1`);
    });

    it('renders a share-scoped image for recipes that have one', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        const { container } = renderPage();

        await screen.findByText('Sunday Roast');

        const images = container.querySelectorAll('img');

        // Only the recipe with hasImage gets an <img>; the other falls back to a placeholder icon.
        expect(images.length).toBe(1);
        expect(images[0].getAttribute('src')).toBe(
            `/api/v1/recipe-collection-share/${shareToken}/recipes/recipe-1/image?w=400`,
        );
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

    it('keeps the app navigation available to a signed-in visitor', async () => {
        useAuth.mockReturnValue({ user: { userId: 'user-1' }, isLoading: false });

        renderPage();

        // One link in the desktop header, one in the mobile tab bar; CSS decides which is visible.
        // Matched loosely: the header link carries the label twice — once visible from lg up, once
        // screen-reader-only below it — and jsdom applies no CSS, so both are in the name here.
        const recipesLinks = await screen.findAllByRole('link', { name: /Recipes/ });

        expect(recipesLinks).toHaveLength(2);
        for (const link of recipesLinks) {
            expect(link.getAttribute('href')).toBe(`/workspaces/${workspace.workspaceId}/`);
        }
        expect(screen.getAllByRole('link', { name: /Shopping/ })).toHaveLength(2);
    });

    it('does not show the app navigation to a signed-out visitor', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        await screen.findByText('Sunday Roast');

        expect(screen.queryAllByRole('link', { name: /Recipes/ })).toHaveLength(0);
    });
});
