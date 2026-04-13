import { useEffect, useMemo, useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { ArrowLeft, Clock, ExternalLink, Flame, Trash2 } from 'lucide-react';
import { motion } from 'framer-motion';
import { recipesApi } from '@/lib/api';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { toast } from '@/hooks/use-toast';
import { Button } from '@/components/ui/button';
import {
    AlertDialog,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { getNutrientAmount, safeHttpUrlHref, scaleRecipeIngredients } from '@/lib/meal-prep';
import { InstructionWithInlineAmounts } from '@/components/recipes/InstructionWithInlineAmounts';
import { RecipeYieldScale } from '@/components/recipes/RecipeYieldScale';
import type { Recipe, RecipeListItem } from '@/models/meal-prep';
import { MealPlanEntryDialog } from '@/components/planner/MealPlanEntryDialog';
import { LoadingState } from '@/components/common/LoadingState';
import { RecipePhotoSection } from '@/components/meal-prep/RecipePhotoSection';

function recipeToListItem(recipe: {
    id: string;
    title: string;
    description?: string | null;
    servings: number;
    isArchived: boolean;
    tags: string[];
    sourceUrl?: string | null;
    hasImage: boolean;
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

    const { data: recipe, isLoading } = useQuery({
        queryKey: ['recipe', workspaceId, recipeId],
        queryFn: () => recipesApi.getById(workspaceId, recipeId),
        enabled: Boolean(workspaceId && recipeId),
    });

    const deleteRecipe = useMutation({
        mutationFn: () => recipesApi.remove(workspaceId, recipeId),
        onSuccess: () => {
            const title = recipe?.title ?? 'Recipe';
            queryClient.removeQueries({ queryKey: ['recipe', workspaceId, recipeId] });
            queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
            queryClient.invalidateQueries({ queryKey: ['meal-plan', workspaceId] });
            capture(
                analyticsEvents.recipeDeleted,
                withWorkspaceProperties(currentWorkspace, { recipe_id: recipeId }),
            );
            toast({
                title: 'Recipe deleted',
                description: `"${title}" was removed from your library.`,
            });
            setDeleteDialogOpen(false);
            navigate(`/workspaces/${workspaceId}/`);
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
        setTargetServings(Math.min(99, Math.max(1, Math.round(base))));
        // Re-sync when navigating to another recipe or the written yield changes — not on refetch.
    }, [recipe?.id, recipe?.servings]); // eslint-disable-line react-hooks/exhaustive-deps -- stable deps; `recipe` omitted to avoid reset on cache refresh

    const baseServings = recipe ? (recipe.servings > 0 ? recipe.servings : 1) : 1;

    const scaledIngredients = useMemo(() => {
        if (!recipe) return [];
        return scaleRecipeIngredients(recipe.ingredients, baseServings, targetServings);
    }, [recipe, baseServings, targetServings]);

    if (isLoading || !recipe) {
        return (
            <div className='mx-auto max-w-3xl px-4 py-10 md:px-8'>
                <LoadingState label='Loading recipe…' />
            </div>
        );
    }

    const totalTime = (recipe.prepMinutes ?? 0) + (recipe.cookMinutes ?? 0);
    const calories = getNutrientAmount(recipe.nutrition, 'calories');
    const protein = getNutrientAmount(recipe.nutrition, 'protein');
    const carbs = getNutrientAmount(recipe.nutrition, 'carbohydrate');
    const fat = getNutrientAmount(recipe.nutrition, 'fat');

    const listItem = recipeToListItem(recipe);
    const cookingPath = `/workspaces/${workspaceId}/cooking/${recipe.id}`;
    const sourceHref = safeHttpUrlHref(recipe.sourceUrl);

    return (
        <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.3 }}
            className='mx-auto max-w-3xl px-4 py-6 md:px-8 md:py-10'
        >
            <div className='mb-6 flex flex-wrap items-center justify-between gap-3'>
                <Link
                    to={`/workspaces/${workspaceId}/`}
                    className='inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground'
                >
                    <ArrowLeft className='h-4 w-4' />
                    Back to recipes
                </Link>
                <Button
                    type='button'
                    variant='outline'
                    className='border-destructive/40 text-destructive hover:bg-destructive/10 hover:text-destructive'
                    onClick={() => setDeleteDialogOpen(true)}
                >
                    <Trash2 className='mr-1.5 h-4 w-4' />
                    Delete
                </Button>
            </div>

            <RecipePhotoSection
                workspaceId={workspaceId}
                recipeId={recipe.id}
                hasImage={recipe.hasImage}
                title={recipe.title}
                onImageChanged={handleRecipeImageChanged}
            />

            <div className='mb-8'>
                <div className='mb-3 flex flex-wrap gap-1.5'>
                    {recipe.tags.map(tag => (
                        <span
                            key={tag}
                            className='rounded-full bg-primary/8 px-2 py-0.5 text-[11px] font-medium uppercase tracking-wider text-primary'
                        >
                            {tag}
                        </span>
                    ))}
                </div>
                <h1 className='font-heading mb-2 text-3xl text-foreground md:text-4xl'>{recipe.title}</h1>
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

            <div className='mb-8 flex gap-3'>
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
                    triggerLabel='Plan this meal'
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
                                    {ing.displayText}
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

            {recipe.nutrition && recipe.nutrition.nutrients.length > 0 && (
                <div className='mt-10'>
                    <h2 className='font-heading mb-4 text-xl text-foreground'>Nutrition per serving</h2>
                    <div className='grid grid-cols-2 gap-3 sm:grid-cols-4'>
                        {[
                            { label: 'Calories', value: calories, unit: 'kcal' },
                            { label: 'Protein', value: protein, unit: 'g' },
                            { label: 'Carbs', value: carbs, unit: 'g' },
                            { label: 'Fat', value: fat, unit: 'g' },
                        ].map(n =>
                            n.value != null ? (
                                <div key={n.label} className='rounded-lg border border-border/50 bg-card p-3 text-center'>
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
        </motion.div>
    );
}
