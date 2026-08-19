// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { TooltipProvider } from '@/components/ui/tooltip';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const getSharedRecipe = vi.fn();
const getShareLinkPreview = vi.fn();
const useAuth = vi.fn();

vi.mock('@/lib/api', () => ({
    recipeCollectionsApi: {
        getSharedRecipe: (shareToken: string, recipeId: string) => getSharedRecipe(shareToken, recipeId),
        getShareLinkPreview: (shareToken: string) => getShareLinkPreview(shareToken),
        sharedRecipeImageUrl: (token: string, recipeId: string, width?: number) =>
            `/api/v1/recipe-collection-share/${token}/recipes/${recipeId}/image${width ? `?w=${width}` : ''}`,
    },
}));

vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => useAuth() }));

const workspace = { workspaceId: 'workspace-1', name: 'Home' };
vi.mock('@/contexts/WorkspaceContext', () => ({
    useWorkspace: () => ({ workspaces: [workspace], currentWorkspace: workspace }),
}));

const { default: SharedRecipeDetailPage } = await import('./SharedRecipeDetailPage');

const shareToken = 'abc123';
const recipeId = 'recipe-1';
const recipePath = `/share/recipe-collections/${shareToken}/recipes/${recipeId}`;

function renderPage() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    return render(
        <QueryClientProvider client={queryClient}>
            <TooltipProvider>
                <MemoryRouter initialEntries={[recipePath]}>
                    <Routes>
                        <Route
                            path='/share/recipe-collections/:shareToken/recipes/:recipeId'
                            element={<SharedRecipeDetailPage />}
                        />
                    </Routes>
                </MemoryRouter>
            </TooltipProvider>
        </QueryClientProvider>,
    );
}

describe('SharedRecipeDetailPage', () => {
    afterEach(() => {
        cleanup();
    });

    beforeEach(() => {
        vi.clearAllMocks();
        getShareLinkPreview.mockResolvedValue({
            collectionName: 'Weeknight Dinners',
            description: null,
            ownerWorkspaceName: 'Sharer Workspace',
            recipeCount: 1,
            recipes: [],
        });
        getSharedRecipe.mockResolvedValue({
            id: recipeId,
            title: 'Sunday Roast',
            description: 'A slow roasted centrepiece',
            servings: 4,
            sourceUrl: 'https://example.test/roast',
            notes: 'Rest before carving',
            prepMinutes: 25,
            cookMinutes: 90,
            tags: ['dinner'],
            hasImage: true,
            ingredients: [
                {
                    id: 'ingredient-1',
                    sortOrder: 0,
                    name: 'Beef brisket',
                    normalizedIngredientName: 'beef brisket',
                    amount: 1.5,
                    unit: 'kg',
                    preparationNote: null,
                    section: null,
                    displayText: '1.5kg beef brisket',
                },
            ],
            steps: [{ id: 'step-1', sortOrder: 0, instruction: 'Season the beef', timerSeconds: null }],
            nutrition: null,
        });
    });

    it('renders the full recipe for a signed-out visitor', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        expect(await screen.findByText('Sunday Roast')).toBeDefined();
        expect(screen.getByText('A slow roasted centrepiece')).toBeDefined();
        expect(screen.getByText(/Beef brisket/)).toBeDefined();
        expect(screen.getByText(/Season the beef/)).toBeDefined();
        expect(screen.getByText('Rest before carving')).toBeDefined();
    });

    it('shows the signup prompt with a return url back to this recipe', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        const signUpLink = await screen.findByRole('link', { name: 'Create free account' });
        const signInLink = screen.getByRole('link', { name: 'Sign in' });

        const expectedReturnUrl = encodeURIComponent(recipePath);
        expect(signUpLink.getAttribute('href')).toBe(`/register?returnUrl=${expectedReturnUrl}`);
        expect(signInLink.getAttribute('href')).toBe(`/login?returnUrl=${expectedReturnUrl}`);
    });

    it('hides the signup prompt once signed in', async () => {
        useAuth.mockReturnValue({ user: { userId: 'user-1' }, isLoading: false });

        renderPage();

        await screen.findByText('Sunday Roast');

        expect(screen.queryByRole('link', { name: 'Create free account' })).toBeNull();
    });

    it('says the recipe is read-only and names who shared it', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        expect(await screen.findByText('Sharer Workspace')).toBeDefined();
        expect(screen.getByText(/but not edit it/)).toBeDefined();
    });

    it('sends a signed-out visitor to sign in before cooking mode', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        const cookLink = await screen.findByRole('link', { name: /Start cooking/ });

        expect(cookLink.getAttribute('href')).toBe(`/login?returnUrl=${encodeURIComponent(recipePath)}`);
    });

    it('links a signed-in visitor straight into cooking mode', async () => {
        useAuth.mockReturnValue({ user: { userId: 'user-1' }, isLoading: false });

        renderPage();

        const cookLink = await screen.findByRole('link', { name: /Start cooking/ });

        expect(cookLink.getAttribute('href')).toBe(`${recipePath}/cooking`);
    });

    it('shows a not-found state when the recipe is not reachable through the token', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });
        getSharedRecipe.mockRejectedValue(new Error('404'));

        renderPage();

        expect(await screen.findByText('Recipe not found')).toBeDefined();
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
