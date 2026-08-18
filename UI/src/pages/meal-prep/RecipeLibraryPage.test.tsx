// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// jsdom has no ResizeObserver; the page uses one to measure its sticky header.
vi.stubGlobal(
    'ResizeObserver',
    class {
        observe() {}
        unobserve() {}
        disconnect() {}
    },
);

const getAll = vi.fn();

vi.mock('@/lib/api', () => ({
    recipesApi: {
        getAll: (workspaceId: string, params?: unknown) => getAll(workspaceId, params),
    },
}));

vi.mock('@/components/meal-prep/RecipeCard', () => ({
    RecipeCard: ({ recipe }: { recipe: { title: string } }) => <div>{recipe.title}</div>,
}));

vi.mock('@/components/recipes/RecipeImportDialog', () => ({
    RecipeImportDialog: ({ trigger }: { trigger: React.ReactNode }) => <>{trigger}</>,
}));

const { default: RecipeLibraryPage } = await import('./RecipeLibraryPage');

const workspaceId = 'workspace-1';

function renderPage() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    return render(
        <QueryClientProvider client={queryClient}>
            <MemoryRouter initialEntries={[`/workspaces/${workspaceId}`]}>
                <Routes>
                    <Route path='/workspaces/:workspaceId' element={<RecipeLibraryPage />} />
                </Routes>
            </MemoryRouter>
        </QueryClientProvider>,
    );
}

describe('RecipeLibraryPage sorting', () => {
    afterEach(() => {
        cleanup();
    });

    beforeEach(() => {
        vi.clearAllMocks();
        window.sessionStorage.clear();
        getAll.mockResolvedValue({
            data: [{ id: 'recipe-1', title: 'Sunday Roast', tags: [], isFavorite: false }],
            page: 1,
            pageSize: 30,
            totalCount: 1,
            totalPages: 1,
        });
    });

    it('requests the newest recipes first by default', async () => {
        renderPage();

        await waitFor(() => expect(getAll).toHaveBeenCalled());
        expect(getAll).toHaveBeenCalledWith(
            workspaceId,
            expect.objectContaining({ orderBy: 'createdAt', direction: 'desc' }),
        );
    });

    it('restores a previously chosen sort field and requests it', async () => {
        window.sessionStorage.setItem(`recipe-library-sort:${workspaceId}`, 'title');

        renderPage();

        await waitFor(() => expect(getAll).toHaveBeenCalled());
        expect(getAll).toHaveBeenCalledWith(
            workspaceId,
            expect.objectContaining({ orderBy: 'title', direction: 'asc' }),
        );
        expect(screen.getByLabelText('Sort by').textContent).toContain('Name');
    });

    it('restores a previously chosen direction, so a reversed list stays reversed', async () => {
        window.sessionStorage.setItem(`recipe-library-sort:${workspaceId}`, 'createdAt');
        window.sessionStorage.setItem(`recipe-library-sort-direction:${workspaceId}`, 'asc');

        renderPage();

        await waitFor(() => expect(getAll).toHaveBeenCalled());
        expect(getAll).toHaveBeenCalledWith(
            workspaceId,
            expect.objectContaining({ orderBy: 'createdAt', direction: 'asc' }),
        );
    });

    it('reverses the list when the direction toggle is pressed, and remembers it', async () => {
        renderPage();

        await waitFor(() => expect(getAll).toHaveBeenCalled());
        fireEvent.click(screen.getByRole('button', { name: /Sort direction/ }));

        await waitFor(() =>
            expect(getAll).toHaveBeenCalledWith(
                workspaceId,
                expect.objectContaining({ orderBy: 'createdAt', direction: 'asc' }),
            ),
        );
        expect(window.sessionStorage.getItem(`recipe-library-sort-direction:${workspaceId}`)).toBe('asc');
    });

    it('falls back to the default sort when the stored values are not known', async () => {
        window.sessionStorage.setItem(`recipe-library-sort:${workspaceId}`, 'nonsense');
        window.sessionStorage.setItem(`recipe-library-sort-direction:${workspaceId}`, 'sideways');

        renderPage();

        await waitFor(() => expect(getAll).toHaveBeenCalled());
        expect(getAll).toHaveBeenCalledWith(
            workspaceId,
            expect.objectContaining({ orderBy: 'createdAt', direction: 'desc' }),
        );
    });
});
