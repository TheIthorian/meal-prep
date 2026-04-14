import { useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useInfiniteQuery } from '@tanstack/react-query';
import { Search, Plus } from 'lucide-react';
import { motion } from 'framer-motion';
import { recipesApi } from '@/lib/api';
import { RecipeCard } from '@/components/meal-prep/RecipeCard';
import { RecipeImportDialog } from '@/components/recipes/RecipeImportDialog';
import type { Recipe } from '@/models/meal-prep';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { formatRecipeTagLabel } from '@/lib/meal-prep';

export default function RecipeLibraryPage() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const navigate = useNavigate();
    const [search, setSearch] = useState('');
    const [activeTag, setActiveTag] = useState<string | null>(null);

    const sentinelRef = useRef<HTMLDivElement | null>(null);

    const {
        data,
        isLoading,
        isFetchingNextPage,
        hasNextPage,
        fetchNextPage,
    } = useInfiniteQuery({
        queryKey: ['recipes', workspaceId, search],
        queryFn: ({ pageParam }) =>
            recipesApi.getAll(workspaceId, {
                q: search.trim() || undefined,
                page: pageParam,
                pageSize: 30,
                includeArchived: false,
            }),
        initialPageParam: 1,
        getNextPageParam: lastPage => {
            // Defensive guard for stale cache entries with unexpected shape.
            if (!lastPage || typeof lastPage !== 'object') return undefined;
            if (!('page' in lastPage) || !('totalPages' in lastPage)) return undefined;

            const page = typeof lastPage.page === 'number' ? lastPage.page : 1;
            const totalPages = typeof lastPage.totalPages === 'number' ? lastPage.totalPages : 1;
            return page < totalPages ? page + 1 : undefined;
        },
        enabled: Boolean(workspaceId),
    });

    useEffect(() => {
        const node = sentinelRef.current;
        if (!node || !hasNextPage) return;

        const observer = new IntersectionObserver(
            entries => {
                const [entry] = entries;
                if (entry?.isIntersecting && hasNextPage && !isFetchingNextPage) {
                    void fetchNextPage();
                }
            },
            { rootMargin: '400px 0px' },
        );

        observer.observe(node);
        return () => observer.disconnect();
    }, [fetchNextPage, hasNextPage, isFetchingNextPage]);

    const recipes = useMemo(() => data?.pages.flatMap(page => page?.data ?? []) ?? [], [data?.pages]);
    const totalCount = data?.pages[0]?.totalCount ?? recipes.length;

    const allTags = useMemo(
        () => Array.from(new Set(recipes.flatMap(r => r.tags))).sort((a, b) => a.localeCompare(b)),
        [recipes],
    );

    const filtered = useMemo(() => {
        if (!activeTag) return recipes;
        return recipes.filter(r => r.tags.includes(activeTag));
    }, [recipes, activeTag]);

    const favourites = useMemo(() => filtered.filter(r => r.isFavorite), [filtered]);
    const otherRecipes = useMemo(() => filtered.filter(r => !r.isFavorite), [filtered]);

    async function handleImported(recipe: Recipe) {
        navigate(`recipe/${recipe.id}`);
    }

    return (
        <div className='mx-auto max-w-6xl px-4 py-6 md:px-8 md:py-10'>
            <motion.div
                initial={{ opacity: 0, y: -8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3 }}
                className='mb-8 flex items-end justify-between'
            >
                <div>
                    <h1 className='font-heading text-3xl text-foreground md:text-4xl'>Recipes</h1>
                    <p className='mt-1 text-muted-foreground'>
                        {data ? `${totalCount} recipes in your collection` : 'Your recipe collection'}
                    </p>
                </div>
                <RecipeImportDialog
                    workspaceId={workspaceId}
                    onImported={handleImported}
                    trigger={
                        <button
                            type='button'
                            className='flex items-center gap-2 rounded-lg bg-primary px-4 py-2.5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90'
                        >
                            <Plus className='h-4 w-4' />
                            <span className='hidden sm:inline'>Add recipe</span>
                        </button>
                    }
                />
            </motion.div>

            <div className='mb-6 space-y-3'>
                <div className='relative'>
                    <Search className='absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground' />
                    <input
                        type='text'
                        placeholder='Search recipes...'
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        className='w-full rounded-lg border border-border bg-card py-2.5 pl-10 pr-4 text-sm text-foreground placeholder:text-muted-foreground transition-all focus:border-primary/40 focus:outline-none focus:ring-2 focus:ring-primary/20'
                    />
                </div>
                {allTags.length > 0 && (
                    <div className='flex flex-wrap gap-2'>
                        {allTags.map(tag => (
                            <button
                                key={tag}
                                type='button'
                                onClick={() => setActiveTag(activeTag === tag ? null : tag)}
                                className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                                    activeTag === tag
                                        ? 'bg-primary text-primary-foreground'
                                        : 'bg-secondary text-secondary-foreground hover:bg-secondary/80'
                                }`}
                            >
                                {formatRecipeTagLabel(tag)}
                            </button>
                        ))}
                    </div>
                )}
            </div>

            {isLoading && <LoadingState label='Loading recipes…' />}

            {!isLoading && filtered.length === 0 && (
                <EmptyState
                    title='No recipes found'
                    description='Try a different search, filter, or add a recipe from a URL.'
                />
            )}

            {!isLoading && favourites.length > 0 && (
                <section className='mb-10'>
                    <h2 className='mb-4 font-heading text-lg text-foreground'>Favourites</h2>
                    <div className='grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3'>
                        {favourites.map((recipe, i) => (
                            <RecipeCard key={recipe.id} workspaceId={workspaceId} recipe={recipe} index={i} />
                        ))}
                    </div>
                </section>
            )}

            {!isLoading && otherRecipes.length > 0 && (
                <section>
                    {favourites.length > 0 ? (
                        <h2 className='mb-4 font-heading text-lg text-foreground'>All recipes</h2>
                    ) : null}
                    <div className='grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3'>
                        {otherRecipes.map((recipe, i) => (
                            <RecipeCard key={recipe.id} workspaceId={workspaceId} recipe={recipe} index={i} />
                        ))}
                    </div>
                </section>
            )}

            {!isLoading && (
                <div ref={sentinelRef} className='h-10'>
                    {isFetchingNextPage ? (
                        <p className='text-center text-sm text-muted-foreground'>Loading more recipes...</p>
                    ) : null}
                </div>
            )}
        </div>
    );
}
