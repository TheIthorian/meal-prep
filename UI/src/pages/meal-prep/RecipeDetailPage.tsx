import { useParams, Link } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Clock, Users, Flame } from 'lucide-react';
import { motion } from 'framer-motion';
import { recipesApi } from '@/lib/api';
import { getNutrientAmount } from '@/lib/meal-prep';
import type { RecipeListItem } from '@/models/meal-prep';
import { MealPlanEntryDialog } from '@/components/planner/MealPlanEntryDialog';
import { LoadingState } from '@/components/common/LoadingState';

function recipeToListItem(recipe: {
    id: string;
    title: string;
    description?: string | null;
    servings: number;
    isArchived: boolean;
    tags: string[];
    sourceUrl?: string | null;
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
        ingredientCount: recipe.ingredients.length,
        stepCount: recipe.steps.length,
    };
}

export default function RecipeDetailPage() {
    const { workspaceId = '', recipeId = '' } = useParams<{ workspaceId: string; recipeId: string }>();
    const queryClient = useQueryClient();

    const { data: recipe, isLoading } = useQuery({
        queryKey: ['recipe', workspaceId, recipeId],
        queryFn: () => recipesApi.getById(workspaceId, recipeId),
        enabled: Boolean(workspaceId && recipeId),
    });

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

    return (
        <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.3 }}
            className='mx-auto max-w-3xl px-4 py-6 md:px-8 md:py-10'
        >
            <Link
                to={`/workspaces/${workspaceId}/`}
                className='mb-6 inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground'
            >
                <ArrowLeft className='h-4 w-4' />
                Back to recipes
            </Link>

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

                <div className='mt-4 flex flex-wrap items-center gap-6 text-sm text-muted-foreground'>
                    {(recipe.prepMinutes ?? recipe.cookMinutes) ? (
                        <span className='flex items-center gap-1.5'>
                            <Clock className='h-4 w-4' />
                            {totalTime > 0 ? `${totalTime} min` : '—'}
                        </span>
                    ) : null}
                    <span className='flex items-center gap-1.5'>
                        <Users className='h-4 w-4' />
                        {recipe.servings} servings
                    </span>
                    {calories != null && (
                        <span className='flex items-center gap-1.5'>
                            <Flame className='h-4 w-4' />
                            {calories} cal
                        </span>
                    )}
                </div>
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
                            {recipe.ingredients.map(ing => (
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
                                <p className='text-sm leading-relaxed text-foreground/90'>{step.instruction}</p>
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
        </motion.div>
    );
}
