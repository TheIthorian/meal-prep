// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TooltipProvider } from '@/components/ui/tooltip';

const getSharedRecipe = vi.fn();
const saveToWorkspace = vi.fn();
const useAuth = vi.fn();
const navigate = vi.fn();

vi.mock('@/lib/api', () => ({
    recipeSharesApi: {
        getSharedRecipe: (shareToken: string) => getSharedRecipe(shareToken),
        saveToWorkspace: (workspaceId: string, shareToken: string) => saveToWorkspace(workspaceId, shareToken),
        sharedRecipeImageUrl: (token: string, width?: number) =>
            `/api/v1/recipe-share/${token}/image${width ? `?w=${width}` : ''}`,
    },
}));

vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => useAuth() }));

const workspace = { workspaceId: 'workspace-1', name: 'Home' };
vi.mock('@/contexts/WorkspaceContext', () => ({
    useWorkspace: () => ({ workspaces: [workspace], currentWorkspace: workspace }),
}));

vi.mock('react-router-dom', async () => {
    const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
    return { ...actual, useNavigate: () => navigate };
});

const { default: SharedRecipePage } = await import('./SharedRecipePage');

const shareToken = 'abc123';
const sharePath = `/share/recipes/${shareToken}`;

function renderPage() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    return render(
        <QueryClientProvider client={queryClient}>
            <TooltipProvider>
                <MemoryRouter initialEntries={[sharePath]}>
                    <Routes>
                        <Route path='/share/recipes/:shareToken' element={<SharedRecipePage />} />
                    </Routes>
                </MemoryRouter>
            </TooltipProvider>
        </QueryClientProvider>,
    );
}

describe('SharedRecipePage', () => {
    afterEach(() => {
        cleanup();
    });

    beforeEach(() => {
        vi.clearAllMocks();
        getSharedRecipe.mockResolvedValue({
            ownerWorkspaceName: 'Sharer Workspace',
            recipe: {
                id: 'recipe-1',
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
            },
        });
    });

    it('renders the full recipe for a signed-out visitor', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        expect(await screen.findByText('Sunday Roast')).toBeDefined();
        expect(screen.getByText(/Beef brisket/)).toBeDefined();
        expect(screen.getByText(/Season the beef/)).toBeDefined();
        expect(screen.getByText('Rest before carving')).toBeDefined();
    });

    it('says the recipe is read-only and names who shared it', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        expect(await screen.findByText('Sharer Workspace')).toBeDefined();
        expect(screen.getByText(/but not edit it/)).toBeDefined();
    });

    it('shows the signup prompt with a return url back to this recipe', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        const signUpLink = await screen.findByRole('link', { name: 'Create free account' });
        const expectedReturnUrl = encodeURIComponent(sharePath);

        expect(signUpLink.getAttribute('href')).toBe(`/register?returnUrl=${expectedReturnUrl}`);
    });

    it('sends a signed-out visitor to sign in before cooking mode', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        const cookLink = await screen.findByRole('link', { name: /Start cooking/ });

        expect(cookLink.getAttribute('href')).toBe(`/login?returnUrl=${encodeURIComponent(sharePath)}`);
    });

    it('does not offer a signed-out visitor the save action', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });

        renderPage();

        await screen.findByText('Sunday Roast');

        expect(screen.queryByRole('button', { name: /Save to my recipes/ })).toBeNull();
    });

    it('links a signed-in visitor straight into cooking mode', async () => {
        useAuth.mockReturnValue({ user: { userId: 'user-1' }, isLoading: false });

        renderPage();

        const cookLink = await screen.findByRole('link', { name: /Start cooking/ });

        expect(cookLink.getAttribute('href')).toBe(`${sharePath}/cooking`);
    });

    it('saves a copy into the workspace and opens it', async () => {
        useAuth.mockReturnValue({ user: { userId: 'user-1' }, isLoading: false });
        saveToWorkspace.mockResolvedValue({ id: 'copied-recipe-1' });

        renderPage();

        const saveButton = await screen.findByRole('button', { name: /Save to my recipes/ });
        fireEvent.click(saveButton);

        await waitFor(() => expect(saveToWorkspace).toHaveBeenCalledWith(workspace.workspaceId, shareToken));
        await waitFor(() =>
            expect(navigate).toHaveBeenCalledWith(`/workspaces/${workspace.workspaceId}/recipe/copied-recipe-1`),
        );
    });

    it('shows a not-found state when the token is not valid', async () => {
        useAuth.mockReturnValue({ user: null, isLoading: false });
        getSharedRecipe.mockRejectedValue(new Error('404'));

        renderPage();

        expect(await screen.findByText('Recipe not found')).toBeDefined();
    });
});
