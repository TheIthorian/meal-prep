import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useParams, useNavigate, useNavigationType, useLocation } from 'react-router-dom';
import { useInfiniteQuery } from '@tanstack/react-query';
import { Search, Plus, ArrowDown, ArrowUp } from 'lucide-react';
import { motion } from 'framer-motion';
import { recipesApi } from '@/lib/api';
import { RecipeCard } from '@/components/meal-prep/RecipeCard';
import { RecipeImportDialog } from '@/components/recipes/RecipeImportDialog';
import type { Recipe } from '@/models/meal-prep';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { formatRecipeTagLabel } from '@/lib/meal-prep';
import { useScrollRestoration } from '@/hooks/use-scroll-restoration';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

/** Set by the recipe detail page's "Back to recipes" link — a push navigation that should
 *  behave like a back navigation. */
interface LibraryLocationState {
    restoreScroll?: boolean;
}

type SortField = 'createdAt' | 'updatedAt' | 'title';
type SortDirection = 'asc' | 'desc';

interface SortFieldOption {
    id: SortField;
    label: string;
    /** The direction a field is most useful in, used when it is first picked. */
    defaultDirection: SortDirection;
    /** What each direction means for this field, so the toggle can say so rather than
     *  leaving the user to work out which way "ascending" runs on a date. */
    ascLabel: string;
    descLabel: string;
}

const SORT_FIELDS: SortFieldOption[] = [
    {
        id: 'createdAt',
        label: 'Date added',
        defaultDirection: 'desc',
        ascLabel: 'Oldest first',
        descLabel: 'Newest first',
    },
    {
        id: 'updatedAt',
        label: 'Last updated',
        defaultDirection: 'desc',
        ascLabel: 'Least recent first',
        descLabel: 'Most recent first',
    },
    { id: 'title', label: 'Name', defaultDirection: 'asc', ascLabel: 'A to Z', descLabel: 'Z to A' },
];

const DEFAULT_SORT_FIELD: SortField = 'createdAt';

function isSortField(value: string): value is SortField {
    return SORT_FIELDS.some(option => option.id === value);
}

function isSortDirection(value: string): value is SortDirection {
    return value === 'asc' || value === 'desc';
}

/** Restoring the scroll position only makes sense if the same list is on screen, so the
 *  filters that shape the list are remembered for the session alongside it. */
function readStoredFilter(key: string): string {
    try {
        return window.sessionStorage.getItem(key) ?? '';
    } catch {
        return '';
    }
}

function writeStoredFilter(key: string, value: string) {
    try {
        window.sessionStorage.setItem(key, value);
    } catch {
        /* ignore */
    }
}

export default function RecipeLibraryPage() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const navigate = useNavigate();
    const navigationType = useNavigationType();
    const location = useLocation();
    const searchStorageKey = `recipe-library-search:${workspaceId}`;
    const tagStorageKey = `recipe-library-tag:${workspaceId}`;
    const sortStorageKey = `recipe-library-sort:${workspaceId}`;
    const sortDirectionStorageKey = `recipe-library-sort-direction:${workspaceId}`;

    // Only a back navigation (or the detail page's "Back to recipes" link) should bring the old
    // filters back. Arriving here fresh — a nav-bar click, a deep link — starts unfiltered, so the
    // list never comes back silently narrowed by a search the user has forgotten about.
    const shouldRestoreFilters =
        navigationType === 'POP' || Boolean((location.state as LibraryLocationState | null)?.restoreScroll);
    const [search, setSearch] = useState(() => (shouldRestoreFilters ? readStoredFilter(searchStorageKey) : ''));
    const [activeTag, setActiveTag] = useState<string | null>(() =>
        shouldRestoreFilters ? readStoredFilter(tagStorageKey) || null : null,
    );
    // The sort is a preference rather than a filter, so it is remembered on every visit — a
    // library the user has set to alphabetical should stay that way after a nav-bar click.
    const [sortField, setSortField] = useState<SortField>(() => {
        const stored = readStoredFilter(sortStorageKey);
        return isSortField(stored) ? stored : DEFAULT_SORT_FIELD;
    });
    const sort = SORT_FIELDS.find(option => option.id === sortField) ?? SORT_FIELDS[0];
    const [sortDirection, setSortDirection] = useState<SortDirection>(() => {
        const stored = readStoredFilter(sortDirectionStorageKey);
        return isSortDirection(stored) ? stored : sort.defaultDirection;
    });

    // Picking a field starts it off in the direction that field is usually wanted in, rather
    // than carrying over a direction that meant something else on the previous field.
    function handleSortFieldChange(value: string) {
        if (!isSortField(value)) return;
        setSortField(value);
        setSortDirection(SORT_FIELDS.find(option => option.id === value)!.defaultDirection);
    }

    const sentinelRef = useRef<HTMLDivElement | null>(null);
    const headerRef = useRef<HTMLDivElement | null>(null);

    // The filters stick at exactly the offset they already sit at, so they hold still rather than
    // sliding up into place as the list scrolls. That offset is the sticky header's height, which
    // moves with the viewport and with the wrapping of the recipe count, so it is measured rather
    // than guessed at.
    useLayoutEffect(() => {
        const node = headerRef.current;
        if (!node) return;

        const root = document.documentElement;
        const apply = () => root.style.setProperty('--recipe-library-header-height', `${node.offsetHeight}px`);
        apply();

        const observer = new ResizeObserver(apply);
        observer.observe(node);
        return () => {
            observer.disconnect();
            root.style.removeProperty('--recipe-library-header-height');
        };
    }, []);

    useEffect(() => {
        writeStoredFilter(searchStorageKey, search);
    }, [searchStorageKey, search]);

    useEffect(() => {
        writeStoredFilter(tagStorageKey, activeTag ?? '');
    }, [tagStorageKey, activeTag]);

    useEffect(() => {
        writeStoredFilter(sortStorageKey, sortField);
    }, [sortStorageKey, sortField]);

    useEffect(() => {
        writeStoredFilter(sortDirectionStorageKey, sortDirection);
    }, [sortDirectionStorageKey, sortDirection]);

    const { data, isLoading, isFetchingNextPage, hasNextPage, fetchNextPage } = useInfiniteQuery({
        queryKey: ['recipes', workspaceId, search, sortField, sortDirection],
        queryFn: ({ pageParam }) =>
            recipesApi.getAll(workspaceId, {
                q: search.trim() || undefined,
                page: pageParam,
                pageSize: 30,
                includeArchived: false,
                orderBy: sortField,
                direction: sortDirection,
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

    // Only going back should land you where you left off — browser back/forward (POP), or the
    // "Back to recipes" link, which pushes a new entry but means the same thing. Arriving from
    // the nav bar or a fresh load starts at the top, with the same condition that decides
    // whether the filters come back.
    const shouldRestoreScroll = shouldRestoreFilters;
    useScrollRestoration(`recipe-library:${workspaceId}`, shouldRestoreScroll && !isLoading && filtered.length > 0);

    useEffect(() => {
        if (!shouldRestoreScroll) window.scrollTo(0, 0);
        // Mount-only: later renders must not yank the page back to the top.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    async function handleImported(recipe: Recipe) {
        navigate(`recipe/${recipe.id}`);
    }

    return (
        <>
            <motion.div
                ref={headerRef}
                initial={{ opacity: 0, y: -8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3 }}
                // The bar keeps "Add recipe" reachable however far down the library you are. It
                // spans the window, with the inner row putting the title back in line with the
                // content below. On desktop it stacks under the app's own nav bar, which is why
                // the offset matches that nav's pinned height (h-[4.5rem] in MealPrepTopNav).
                className='sticky top-0 z-20 mb-8 border-b border-border/60 bg-background/80 py-4 backdrop-blur-md md:top-[4.5rem]'
            >
                {/* Mirrors the content container below exactly, so the title lines up with the grid. */}
                <div className='mx-auto flex max-w-6xl items-end justify-between gap-4 px-4 md:px-8 xl:max-w-7xl'>
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
                                // The label is hidden below the sm breakpoint, leaving the icon alone
                                // and the button with no accessible name on mobile.
                                aria-label='Add recipe'
                                className='flex shrink-0 items-center gap-2 rounded-lg bg-primary px-4 py-2.5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90'
                            >
                                <Plus className='h-4 w-4' />
                                <span className='hidden sm:inline'>Add recipe</span>
                            </button>
                        }
                    />
                </div>
            </motion.div>

            <div className='mx-auto max-w-6xl px-4 pb-6 md:px-8 md:pb-10 xl:max-w-7xl'>
                <div className='lg:grid lg:grid-cols-3 lg:gap-8'>
                    <aside
                        aria-label='Search and filters'
                        // The offset is where this column already starts: the app nav, then this
                        // page's header bar, then the gap below it. Sticking there means the filters
                        // never move. The horizontal padding, cancelled by the matching negative
                        // margin, keeps this scroll container from slicing the focus ring off the
                        // full-width controls inside it. The ring is drawn outside their border box.
                        className='mb-6 space-y-3 lg:sticky lg:top-[calc(4.5rem+var(--recipe-library-header-height,6.25rem)+2rem)] lg:col-span-1 lg:mb-0 lg:max-h-[calc(100vh-4.5rem-var(--recipe-library-header-height,6.25rem)-3rem)] lg:-mx-1.5 lg:self-start lg:overflow-y-auto lg:px-1.5'
                    >
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
                        <div className='space-y-1.5'>
                            <label htmlFor='recipe-library-sort' className='text-xs font-medium text-muted-foreground'>
                                Sort by
                            </label>
                            <div className='flex gap-2'>
                                <Select value={sortField} onValueChange={handleSortFieldChange}>
                                    <SelectTrigger id='recipe-library-sort' className='flex-1'>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {SORT_FIELDS.map(option => (
                                            <SelectItem key={option.id} value={option.id}>
                                                {option.label}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                <button
                                    type='button'
                                    onClick={() => setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc')}
                                    // The button both reports the current direction and flips it, so the
                                    // name says which way the list runs now rather than what the click does.
                                    aria-label={`Sort direction: ${
                                        sortDirection === 'asc' ? sort.ascLabel : sort.descLabel
                                    }. Reverse it.`}
                                    title={sortDirection === 'asc' ? sort.ascLabel : sort.descLabel}
                                    className='flex h-10 w-10 shrink-0 items-center justify-center rounded-md border border-input bg-background text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 focus:ring-offset-background'
                                >
                                    {sortDirection === 'asc' ? (
                                        <ArrowUp className='h-4 w-4' />
                                    ) : (
                                        <ArrowDown className='h-4 w-4' />
                                    )}
                                </button>
                            </div>
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
                    </aside>

                    <div className='min-w-0 lg:col-span-2'>
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
                                <div className='grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3'>
                                    {favourites.map((recipe, i) => (
                                        <RecipeCard
                                            key={recipe.id}
                                            workspaceId={workspaceId}
                                            recipe={recipe}
                                            index={i}
                                        />
                                    ))}
                                </div>
                            </section>
                        )}

                        {!isLoading && otherRecipes.length > 0 && (
                            <section>
                                {/* Always rendered so the cards' h3 never follows the page h1 directly, which
                        breaks the heading outline for screen readers. Hidden when there is no
                        Favourites section above it to distinguish this one from. */}
                                <h2
                                    className={
                                        favourites.length > 0 ? 'mb-4 font-heading text-lg text-foreground' : 'sr-only'
                                    }
                                >
                                    All recipes
                                </h2>
                                <div className='grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3'>
                                    {otherRecipes.map((recipe, i) => (
                                        <RecipeCard
                                            key={recipe.id}
                                            workspaceId={workspaceId}
                                            recipe={recipe}
                                            index={i}
                                        />
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
                </div>
            </div>
        </>
    );
}
