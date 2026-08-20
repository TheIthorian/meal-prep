import { useEffect, useMemo, useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import {
    ArrowLeft,
    ChevronLeft,
    ChevronRight,
    Clock,
    Pencil,
    ExternalLink,
    Flame,
    Share2,
    Sparkles,
    Star,
    Trash2,
} from 'lucide-react';
import { motion } from 'framer-motion';
import { recipesApi } from '@/lib/api';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { toast } from '@/hooks/use-toast';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import {
    AlertDialog,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { formatRecipeTagLabel, getNutrientAmount, safeHttpUrlHref, scaleRecipeIngredients } from '@/lib/meal-prep';
import { toSaveRecipeRequest } from '@/lib/meal-prep';
import { InstructionWithInlineAmounts } from '@/components/recipes/InstructionWithInlineAmounts';
import { RecipeIngredientListRow } from '@/components/recipes/RecipeIngredientListRow';
import { RecipeYieldScale } from '@/components/recipes/RecipeYieldScale';
import type { Recipe, RecipeListItem } from '@/models/meal-prep';
import { MealPlanEntryDialog } from '@/components/planner/MealPlanEntryDialog';
import { RecipeDetailSkeleton } from '@/components/recipes/RecipeDetailSkeleton';
import { RecipePhotoSection } from '@/components/meal-prep/RecipePhotoSection';
import { AddToRecipeCollectionMenu } from '@/components/meal-prep/AddToRecipeCollectionMenu';
import { RecipeForm } from '@/components/recipes/RecipeForm';
import { ShareRecipeDialog } from '@/components/recipes/ShareRecipeDialog';

function shouldSuppressRecipeArrowNavigation(target: EventTarget | null): boolean {
    if (!(target instanceof HTMLElement)) return false;
    const tag = target.tagName;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return true;
    if (target.isContentEditable) return true;
    if (target.closest('[role="dialog"]')) return true;
    return false;
}

interface RecipeNeighbors {
    showNav: boolean;
    prevId: string | null;
    nextId: string | null;
}

const NO_RECIPE_NEIGHBORS: RecipeNeighbors = { showNav: false, prevId: null, nextId: null };

const NEIGHBOR_PAGE_SIZE = 100;

/**
 * Finds the recipes either side of `recipeId` in the library's default order, paging until the
 * recipe turns up. Pages are fetched one at a time so a library that fits on the first page
 * still costs a single request.
 */
async function findRecipeNeighbors(workspaceId: string, recipeId: string): Promise<RecipeNeighbors> {
    let page = 1;
    let totalPages = 1;
    // Last id of the previous page, which is the previous neighbour when the recipe is first on its page.
    let idBeforePage: string | null = null;

    while (page <= totalPages) {
        const result = await recipesApi.getAll(workspaceId, {
            page,
            pageSize: NEIGHBOR_PAGE_SIZE,
            includeArchived: false,
        });
        const list = result.data ?? [];
        totalPages = result.totalPages ?? 1;

        const index = list.findIndex(item => item.id === recipeId);
        if (index >= 0) {
            const prevId = index > 0 ? (list[index - 1]?.id ?? null) : idBeforePage;
            let nextId = index < list.length - 1 ? (list[index + 1]?.id ?? null) : null;

            if (nextId === null && page < totalPages) {
                const nextPage = await recipesApi.getAll(workspaceId, {
                    page: page + 1,
                    pageSize: NEIGHBOR_PAGE_SIZE,
                    includeArchived: false,
                });
                nextId = nextPage.data?.[0]?.id ?? null;
            }

            return { showNav: prevId !== null || nextId !== null, prevId, nextId };
        }

        idBeforePage = list.at(-1)?.id ?? idBeforePage;
        page += 1;
    }

    return NO_RECIPE_NEIGHBORS;
}

function recipeToListItem(recipe: {
    id: string;
    title: string;
    description?: string | null;
    servings: number;
    isArchived: boolean;
    tags: string[];
    sourceUrl?: string | null;
    hasImage: boolean;
    isFavorite: boolean;
    ingredients: unknown[];
    steps: unknown[];
}): RecipeListItem {
    return {
        id: recipe.id,
        title: recipe.title,
        description: recipe.description ?? undefined,
        servings: recipe.servings,
        isArchived: recipe.isArchived,
        tags: recipe.tags,
        sourceUrl: recipe.sourceUrl,
        hasImage: recipe.hasImage,
        isFavorite: recipe.isFavorite,
        ingredientCount: recipe.ingredients.length,
        stepCount: recipe.steps.length,
    };
}

export default function RecipeDetailPage() {
    const { workspaceId = '', recipeId = '' } = useParams<{ workspaceId: string; recipeId: string }>();
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [shareDialogOpen, setShareDialogOpen] = useState(false);
    const [isEditing, setIsEditing] = useState(false);
    const [draftRecipe, setDraftRecipe] = useState<Recipe | null>(null);

    const {
        data: recipe,
        isLoading,
        isError,
        error,
        refetch,
    } = useQuery({
        queryKey: ['recipe', workspaceId, recipeId],
        queryFn: () => recipesApi.getById(workspaceId, recipeId),
        enabled: Boolean(workspaceId && recipeId),
    });

    /**
     * Same request as the library with an empty search — order matches prev/next in the grid
     * (title, API default). Only the first page used to be fetched, so anything past it had no
     * neighbours at all; walk pages until the recipe is found instead.
     */
    const { data: recipeNeighbors = NO_RECIPE_NEIGHBORS } = useQuery({
        // Keep this key distinct from the library's infinite query to avoid cache-shape collisions.
        queryKey: ['recipe-neighbors', workspaceId, recipeId],
        queryFn: () => findRecipeNeighbors(workspaceId, recipeId),
        enabled: Boolean(workspaceId && recipeId),
    });

    useEffect(() => {
        if (!workspaceId || !recipeId) return;

        function onKeyDown(e: KeyboardEvent) {
            if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
            if (deleteDialogOpen) return;
            if (shouldSuppressRecipeArrowNavigation(e.target)) return;
            if (!recipeNeighbors.showNav) return;

            if (e.key === 'ArrowLeft' && recipeNeighbors.prevId) {
                e.preventDefault();
                navigate(`/workspaces/${workspaceId}/recipe/${recipeNeighbors.prevId}`);
                return;
            }
            if (e.key === 'ArrowRight' && recipeNeighbors.nextId) {
                e.preventDefault();
                navigate(`/workspaces/${workspaceId}/recipe/${recipeNeighbors.nextId}`);
            }
        }

        window.addEventListener('keydown', onKeyDown);
        return () => window.removeEventListener('keydown', onKeyDown);
    }, [
        workspaceId,
        recipeId,
        recipeNeighbors.showNav,
        recipeNeighbors.prevId,
        recipeNeighbors.nextId,
        navigate,
        deleteDialogOpen,
    ]);

    useEffect(() => {
        setDeleteDialogOpen(false);
        setIsEditing(false);
        setDraftRecipe(null);
    }, [recipeId]);

    const setFavoriteRecipe = useMutation({
        mutationFn: (next: boolean) => recipesApi.setFavorite(workspaceId, recipeId, next),
        onSuccess: updated => {
            queryClient.setQueryData<Recipe>(['recipe', workspaceId, recipeId], updated);
            void queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeFavoriteUpdated,
                    withWorkspaceProperties(currentWorkspace, {
                        recipe_id: recipeId,
                        is_favorite: updated.isFavorite,
                    }),
                );
            }
        },
    });

    const autotagRecipe = useMutation({
        mutationFn: () => recipesApi.autotag(workspaceId, recipeId),
        onSuccess: updated => {
            queryClient.setQueryData<Recipe>(['recipe', workspaceId, recipeId], updated);
            void queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-tag-usage', workspaceId] });
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeAutotagged,
                    withWorkspaceProperties(currentWorkspace, {
                        recipe_id: recipeId,
                        tag_count: updated.tags.length,
                    }),
                );
            }
            toast({
                title: 'Tags updated',
                description:
                    updated.tags.length > 0
                        ? `Saved ${updated.tags.length} tag${updated.tags.length === 1 ? '' : 's'} from the allowed list.`
                        : 'No matching tags were applied.',
            });
        },
        onError: () => {
            toast({
                title: 'Auto-tag failed',
                description: 'Check that AI is configured, or try again in a moment.',
                variant: 'destructive',
            });
        },
    });

    const deleteRecipe = useMutation({
        mutationFn: () => recipesApi.remove(workspaceId, recipeId),
        onSuccess: () => {
            const title = recipe?.title ?? 'Recipe';
            queryClient.removeQueries({ queryKey: ['recipe', workspaceId, recipeId] });
            queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
            queryClient.invalidateQueries({ queryKey: ['meal-plan', workspaceId] });
            capture(analyticsEvents.recipeDeleted, withWorkspaceProperties(currentWorkspace, { recipe_id: recipeId }));
            toast({
                title: 'Recipe deleted',
                description: `"${title}" was removed from your library.`,
            });
            setDeleteDialogOpen(false);
            navigate(`/workspaces/${workspaceId}/`);
        },
    });

    const updateRecipe = useMutation({
        mutationFn: (nextRecipe: Recipe) => recipesApi.update(workspaceId, recipeId, toSaveRecipeRequest(nextRecipe)),
        onSuccess: updated => {
            queryClient.setQueryData<Recipe>(['recipe', workspaceId, recipeId], updated);
            void queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
            setIsEditing(false);
            setDraftRecipe(null);
            toast({ title: 'Recipe updated' });
        },
        onError: () => {
            toast({
                title: 'Failed to save recipe',
                description: 'Please review the recipe fields and try again.',
                variant: 'destructive',
            });
        },
    });

    function handleRecipeImageChanged(nextHasImage: boolean) {
        queryClient.setQueryData<Recipe>(['recipe', workspaceId, recipeId], previous =>
            previous ? { ...previous, hasImage: nextHasImage } : previous,
        );
        void queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
    }

    const [targetServings, setTargetServings] = useState(1);

    useEffect(() => {
        if (!recipe) return;
        const base = recipe.servings > 0 ? recipe.servings : 1;
        setTargetServings(Math.min(99, base));
        // Re-sync when navigating to another recipe or the written yield changes — not on refetch.
    }, [recipe?.id, recipe?.servings]); // eslint-disable-line react-hooks/exhaustive-deps -- stable deps; `recipe` omitted to avoid reset on cache refresh

    const baseServings = recipe ? (recipe.servings > 0 ? recipe.servings : 1) : 1;

    const scaledIngredients = useMemo(() => {
        if (!recipe) return [];
        return scaleRecipeIngredients(recipe.ingredients, baseServings, targetServings);
    }, [recipe, baseServings, targetServings]);

    if (isError) {
        const description = error instanceof Error ? error.message : 'Something went wrong while loading this recipe.';
        return (
            <div className='mx-auto max-w-3xl px-4 py-10 md:px-8'>
                <div className='rounded-xl border border-destructive/30 bg-card p-5'>
                    <h2 className='font-heading text-xl text-foreground'>Could not load recipe</h2>
                    <p className='mt-2 text-sm text-muted-foreground'>{description}</p>
                    <div className='mt-4 flex gap-2'>
                        <Button type='button' variant='outline' onClick={() => void refetch()}>
                            Try again
                        </Button>
                        <Button type='button' variant='ghost' asChild>
                            <Link to={`/workspaces/${workspaceId}/`}>Back to recipes</Link>
                        </Button>
                    </div>
                </div>
            </div>
        );
    }

    if (isLoading || !recipe) {
        return <RecipeDetailSkeleton />;
    }

    const totalTime = (recipe.prepMinutes ?? 0) + (recipe.cookMinutes ?? 0);
    const calories = getNutrientAmount(recipe.nutrition, 'calories');
    const protein = getNutrientAmount(recipe.nutrition, 'protein');
    const carbs = getNutrientAmount(recipe.nutrition, 'carbohydrate');
    const fat = getNutrientAmount(recipe.nutrition, 'fat');

    const listItem = recipeToListItem(recipe);
    const cookingPath = `/workspaces/${workspaceId}/cooking/${recipe.id}`;
    const sourceHref = safeHttpUrlHref(recipe.sourceUrl);

    function startEditing() {
        setDraftRecipe(structuredClone(recipe));
        setIsEditing(true);
    }

    function cancelEditing() {
        setIsEditing(false);
        setDraftRecipe(null);
    }

    if (isEditing && draftRecipe) {
        const saveDraft = () => void updateRecipe.mutateAsync(draftRecipe);

        return (
            <div className='mx-auto max-w-5xl px-4 py-6 md:px-8 md:py-10'>
                <div className='mb-6 flex flex-wrap items-center justify-between gap-3'>
                    <h1 className='font-heading text-2xl text-foreground md:text-3xl'>Edit Recipe</h1>
                    <div className='flex items-center gap-3'>
                        <Button type='button' variant='ghost' onClick={cancelEditing} disabled={updateRecipe.isPending}>
                            Cancel
                        </Button>
                        <Button type='button' onClick={saveDraft} disabled={updateRecipe.isPending}>
                            {updateRecipe.isPending ? 'Saving...' : 'Save Recipe'}
                        </Button>
                    </div>
                </div>
                <RecipeForm
                    recipe={draftRecipe}
                    workspaceId={workspaceId}
                    isSaving={updateRecipe.isPending}
                    onChange={setDraftRecipe}
                    onSubmit={saveDraft}
                />
            </div>
        );
    }

    return (
        <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.3 }}
            className='mx-auto max-w-3xl px-4 py-6 md:px-8 md:py-10 xl:max-w-7xl'
        >
            <div className='mb-6 flex flex-wrap items-center justify-between gap-3'>
                <div className='flex flex-wrap items-center gap-2'>
                    <Link
                        to={`/workspaces/${workspaceId}/`}
                        // Pushes a new entry rather than popping, so tell the library this is a
                        // "back" and it should restore where the user was scrolled to.
                        state={{ restoreScroll: true }}
                        className='inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground'
                    >
                        <ArrowLeft className='h-4 w-4' />
                        Back to recipes
                    </Link>
                    {recipeNeighbors.showNav ? (
                        <div className='flex items-center gap-1 border-l border-border/60 pl-2'>
                            {recipeNeighbors.prevId ? (
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <Button asChild variant='outline' size='icon' className='h-9 w-9 shrink-0'>
                                            <Link
                                                to={`/workspaces/${workspaceId}/recipe/${recipeNeighbors.prevId}`}
                                                aria-label='Previous recipe'
                                            >
                                                <ChevronLeft className='h-4 w-4' />
                                            </Link>
                                        </Button>
                                    </TooltipTrigger>
                                    <TooltipContent side='bottom'>Previous recipe</TooltipContent>
                                </Tooltip>
                            ) : (
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <Button
                                            type='button'
                                            variant='outline'
                                            size='icon'
                                            className='h-9 w-9 shrink-0'
                                            disabled
                                            aria-label='Previous recipe'
                                        >
                                            <ChevronLeft className='h-4 w-4' />
                                        </Button>
                                    </TooltipTrigger>
                                    <TooltipContent side='bottom'>Previous recipe</TooltipContent>
                                </Tooltip>
                            )}
                            {recipeNeighbors.nextId ? (
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <Button asChild variant='outline' size='icon' className='h-9 w-9 shrink-0'>
                                            <Link
                                                to={`/workspaces/${workspaceId}/recipe/${recipeNeighbors.nextId}`}
                                                aria-label='Next recipe'
                                            >
                                                <ChevronRight className='h-4 w-4' />
                                            </Link>
                                        </Button>
                                    </TooltipTrigger>
                                    <TooltipContent side='bottom'>Next recipe</TooltipContent>
                                </Tooltip>
                            ) : (
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <Button
                                            type='button'
                                            variant='outline'
                                            size='icon'
                                            className='h-9 w-9 shrink-0'
                                            disabled
                                            aria-label='Next recipe'
                                        >
                                            <ChevronRight className='h-4 w-4' />
                                        </Button>
                                    </TooltipTrigger>
                                    <TooltipContent side='bottom'>Next recipe</TooltipContent>
                                </Tooltip>
                            )}
                        </div>
                    ) : null}
                </div>
                <div className='flex flex-wrap items-center gap-2'>
                    <Tooltip>
                        <TooltipTrigger asChild>
                            <Button
                                type='button'
                                variant='outline'
                                size='icon'
                                className='h-9 w-9 shrink-0 text-amber-600'
                                disabled={setFavoriteRecipe.isPending}
                                aria-label={recipe.isFavorite ? 'Remove from favourites' : 'Add to favourites'}
                                aria-pressed={recipe.isFavorite}
                                onClick={() => void setFavoriteRecipe.mutateAsync(!recipe.isFavorite)}
                            >
                                <Star
                                    className={`h-4 w-4 ${recipe.isFavorite ? 'fill-amber-400 text-amber-500' : ''}`}
                                />
                            </Button>
                        </TooltipTrigger>
                        <TooltipContent side='bottom'>
                            {recipe.isFavorite ? 'Remove from favourites' : 'Add to favourites'}
                        </TooltipContent>
                    </Tooltip>
                    <AddToRecipeCollectionMenu workspaceId={workspaceId} recipeId={recipeId} variant='compact' />
                    <Tooltip>
                        <TooltipTrigger asChild>
                            <Button
                                type='button'
                                variant='outline'
                                size='icon'
                                className='h-9 w-9 shrink-0'
                                aria-label='Share recipe'
                                onClick={() => setShareDialogOpen(true)}
                            >
                                <Share2 className='h-4 w-4' />
                            </Button>
                        </TooltipTrigger>
                        <TooltipContent side='bottom'>Share recipe</TooltipContent>
                    </Tooltip>
                    <Tooltip>
                        <TooltipTrigger asChild>
                            <Button
                                type='button'
                                variant='outline'
                                size='icon'
                                className='h-9 w-9 shrink-0'
                                aria-label='Edit recipe'
                                onClick={startEditing}
                            >
                                <Pencil className='h-4 w-4' />
                            </Button>
                        </TooltipTrigger>
                        <TooltipContent side='bottom'>Edit recipe</TooltipContent>
                    </Tooltip>
                    <Tooltip>
                        <TooltipTrigger asChild>
                            <Button
                                type='button'
                                variant='outline'
                                size='icon'
                                className='h-9 w-9 shrink-0 border-destructive/40 text-destructive hover:bg-destructive/10 hover:text-destructive'
                                aria-label='Delete recipe'
                                onClick={() => setDeleteDialogOpen(true)}
                            >
                                <Trash2 className='h-4 w-4' />
                            </Button>
                        </TooltipTrigger>
                        <TooltipContent side='bottom'>Delete recipe</TooltipContent>
                    </Tooltip>
                </div>
            </div>

            <div className='xl:grid xl:grid-cols-[minmax(0,26rem)_minmax(0,1fr)] xl:items-start xl:gap-12'>
                <div className='min-w-0'>
                    <div className='mb-6'>
                        <div className='mb-3 flex flex-wrap items-center gap-2'>
                            <div className='flex min-w-0 flex-1 flex-wrap gap-1.5'>
                                {recipe.tags.length === 0 ? (
                                    <span className='text-sm text-muted-foreground'>No tags yet</span>
                                ) : (
                                    recipe.tags.map(tag => (
                                        <span
                                            key={tag}
                                            className='rounded-full bg-primary/8 px-2 py-0.5 text-xs font-medium text-primary'
                                        >
                                            {formatRecipeTagLabel(tag)}
                                        </span>
                                    ))
                                )}
                            </div>
                            <Tooltip>
                                <TooltipTrigger asChild>
                                    <Button
                                        type='button'
                                        variant='outline'
                                        size='icon'
                                        className='h-9 w-9 shrink-0'
                                        disabled={autotagRecipe.isPending}
                                        aria-label='Auto-tag with AI'
                                        onClick={() => void autotagRecipe.mutateAsync()}
                                    >
                                        <Sparkles className='h-4 w-4' />
                                    </Button>
                                </TooltipTrigger>
                                <TooltipContent side='bottom'>
                                    Auto-tag with AI (uses title, description, ingredients, and steps)
                                </TooltipContent>
                            </Tooltip>
                        </div>
                        <h1 className='font-heading text-3xl text-foreground md:text-4xl'>{recipe.title}</h1>
                        {(recipe.collections?.length ?? 0) > 0 ? (
                            <div className='mt-3 flex flex-wrap gap-2'>
                                {recipe.collections!.map(collection => (
                                    <Link
                                        key={collection.collectionId}
                                        to={`/workspaces/${collection.ownerWorkspaceId}/collections/${collection.collectionId}`}
                                        className='rounded-full border border-border bg-card px-3 py-1 text-xs font-medium text-foreground transition-colors hover:bg-secondary'
                                    >
                                        {collection.collectionName}
                                    </Link>
                                ))}
                            </div>
                        ) : null}
                    </div>

                    <RecipePhotoSection
                        workspaceId={workspaceId}
                        recipeId={recipe.id}
                        hasImage={recipe.hasImage}
                        title={recipe.title}
                        onImageChanged={handleRecipeImageChanged}
                    />

                    <div className='mb-8'>
                        <p className='text-muted-foreground'>{recipe.description ?? '—'}</p>

                        {recipe.sourceUrl ? (
                            <div className='mt-3'>
                                {sourceHref ? (
                                    <a
                                        href={sourceHref}
                                        target='_blank'
                                        rel='noopener noreferrer'
                                        className='inline-flex items-center gap-1.5 text-sm font-medium text-primary hover:underline'
                                    >
                                        <ExternalLink className='h-4 w-4 shrink-0' />
                                        View original recipe
                                    </a>
                                ) : (
                                    <p className='text-sm text-muted-foreground break-all'>
                                        <span className='font-medium text-foreground'>Source: </span>
                                        {recipe.sourceUrl}
                                    </p>
                                )}
                            </div>
                        ) : null}

                        <div className='mt-4 flex flex-wrap items-center gap-6 text-sm text-muted-foreground'>
                            {(recipe.prepMinutes ?? recipe.cookMinutes) ? (
                                <span className='flex items-center gap-1.5'>
                                    <Clock className='h-4 w-4' />
                                    {totalTime > 0 ? `${totalTime} min` : '—'}
                                </span>
                            ) : null}
                            {calories != null && (
                                <span className='flex items-center gap-1.5'>
                                    <Flame className='h-4 w-4' />
                                    {calories} cal
                                </span>
                            )}
                        </div>

                        <RecipeYieldScale
                            className='mt-5'
                            baseServings={baseServings}
                            targetServings={targetServings}
                            onTargetServingsChange={setTargetServings}
                        />
                    </div>

                    {recipe.nutrition && recipe.nutrition.nutrients.length > 0 && (
                        <div className='mb-8'>
                            <h2 className='font-heading mb-4 text-xl text-foreground'>Nutrition per serving</h2>
                            <div className='grid grid-cols-2 gap-3 sm:grid-cols-4 xl:grid-cols-2'>
                                {[
                                    { label: 'Calories', value: calories, unit: 'kcal' },
                                    { label: 'Protein', value: protein, unit: 'g' },
                                    { label: 'Carbs', value: carbs, unit: 'g' },
                                    { label: 'Fat', value: fat, unit: 'g' },
                                ].map(n =>
                                    n.value != null ? (
                                        <div
                                            key={n.label}
                                            className='rounded-lg border border-border/50 bg-card p-3 text-center'
                                        >
                                            <p className='text-lg font-semibold tabular-nums text-foreground'>
                                                {n.value}
                                                <span className='ml-0.5 text-xs text-muted-foreground'>{n.unit}</span>
                                            </p>
                                            <p className='mt-0.5 text-xs text-muted-foreground'>{n.label}</p>
                                        </div>
                                    ) : null,
                                )}
                            </div>
                        </div>
                    )}
                </div>

                <div className='min-w-0'>
                    <div className='mb-8 flex items-center gap-3'>
                        <Link
                            to={cookingPath}
                            className='flex-1 rounded-lg bg-primary px-4 py-3 text-center text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90'
                        >
                            Start cooking
                        </Link>
                        <MealPlanEntryDialog
                            workspaceId={workspaceId}
                            recipes={[listItem]}
                            selectedDate={new Date().toISOString().slice(0, 10)}
                            triggerLabel='Add to next meals'
                            onSaved={() => {
                                queryClient.invalidateQueries({ queryKey: ['meal-plan', workspaceId] });
                            }}
                        />
                    </div>

                    <div className='grid gap-8 md:grid-cols-[280px_1fr]'>
                        <div>
                            <h2 className='font-heading mb-4 text-xl text-foreground'>Ingredients</h2>
                            <div className='rounded-xl border border-border/50 bg-card p-4'>
                                <ul className='space-y-2.5'>
                                    {scaledIngredients.map(ing => (
                                        <li key={ing.id} className='text-sm text-foreground/90'>
                                            <RecipeIngredientListRow ingredient={ing} />
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        </div>

                        <div>
                            <h2 className='font-heading mb-4 text-xl text-foreground'>Instructions</h2>
                            <ol className='space-y-4'>
                                {recipe.steps.map((step, i) => (
                                    <motion.li
                                        key={step.id}
                                        initial={{ opacity: 0, y: 8 }}
                                        animate={{ opacity: 1, y: 0 }}
                                        transition={{ delay: i * 0.06, duration: 0.3 }}
                                        className='flex gap-3'
                                    >
                                        <span className='mt-0.5 flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary'>
                                            {i + 1}
                                        </span>
                                        <p className='text-sm leading-relaxed text-foreground/90'>
                                            <InstructionWithInlineAmounts
                                                instruction={step.instruction}
                                                scaledIngredients={scaledIngredients}
                                            />
                                        </p>
                                    </motion.li>
                                ))}
                            </ol>
                        </div>
                    </div>
                </div>
            </div>

            <AlertDialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Delete this recipe?</AlertDialogTitle>
                        <AlertDialogDescription>
                            This removes &quot;{recipe.title}&quot; from your library. You can import or add it again
                            later if you change your mind.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel disabled={deleteRecipe.isPending}>Cancel</AlertDialogCancel>
                        <Button
                            variant='destructive'
                            disabled={deleteRecipe.isPending}
                            onClick={() => deleteRecipe.mutate()}
                        >
                            {deleteRecipe.isPending ? 'Deleting…' : 'Delete recipe'}
                        </Button>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

            <ShareRecipeDialog
                open={shareDialogOpen}
                onOpenChange={setShareDialogOpen}
                workspaceId={workspaceId}
                recipeId={recipe.id}
                recipeTitle={recipe.title}
            />
        </motion.div>
    );
}
