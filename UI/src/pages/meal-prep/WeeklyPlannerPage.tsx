import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { motion } from 'framer-motion';
import { CheckCircle2, Circle } from 'lucide-react';
import { mealPlanApi, recipesApi } from '@/lib/api';
import { formatDateLabel } from '@/lib/meal-prep';
import type { MealPlanEntry } from '@/models/meal-prep';
import { MealPlanEntryDialog } from '@/components/planner/MealPlanEntryDialog';
import { LoadingState } from '@/components/common/LoadingState';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

const mealTypes = ['breakfast', 'lunch', 'dinner', 'snack'] as const;
const mealLabels: Record<(typeof mealTypes)[number], string> = {
    breakfast: 'Breakfast',
    lunch: 'Lunch',
    dinner: 'Dinner',
    snack: 'Snack',
};

export default function WeeklyPlannerPage() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const queryClient = useQueryClient();

    const { data: entries = [], isLoading: entriesLoading } = useQuery({
        queryKey: ['meal-plan', workspaceId],
        queryFn: () => mealPlanApi.getAll(workspaceId),
        enabled: Boolean(workspaceId),
    });

    const { data: recipesPage, isLoading: recipesLoading } = useQuery({
        queryKey: ['recipes', workspaceId, 'planner-select'],
        queryFn: () => recipesApi.getAll(workspaceId, { page: 1, pageSize: 100, includeArchived: false }),
        enabled: Boolean(workspaceId),
    });

    const recipes = recipesPage?.data ?? [];

    function invalidatePlan() {
        queryClient.invalidateQueries({ queryKey: ['meal-plan', workspaceId] });
    }

    const toggleCompleted = useMutation({
        mutationFn: async (entry: MealPlanEntry) => {
            const nextDone = entry.status !== 'completed';
            return mealPlanApi.update(workspaceId, entry.id, {
                recipeId: entry.recipeId,
                plannedDate: entry.plannedDate,
                mealType: entry.mealType,
                targetServings: entry.targetServings ?? null,
                notes: entry.notes ?? null,
                status: nextDone ? 'completed' : 'planned',
                completedAtUtc: nextDone ? new Date().toISOString() : null,
            });
        },
        onSuccess: invalidatePlan,
    });

    const isLoading = entriesLoading || recipesLoading;

    return (
        <div className='mx-auto max-w-6xl px-4 py-6 md:px-8 md:py-10'>
            <motion.div
                initial={{ opacity: 0, y: -8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3 }}
                className='mb-8'
            >
                <h1 className='font-heading text-3xl text-foreground md:text-4xl'>Next meals</h1>
                <p className='mt-1 text-muted-foreground'>Build a flexible queue of upcoming meals</p>
            </motion.div>

            <div className='mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border/60 bg-card p-4'>
                <div>
                    <h2 className='text-sm font-semibold uppercase tracking-wide text-muted-foreground'>Add next meal</h2>
                    <p className='text-sm text-muted-foreground'>Pick a recipe to add it to your next meals queue.</p>
                </div>
                <MealPlanEntryDialog
                    workspaceId={workspaceId}
                    recipes={recipes}
                    triggerLabel='Add next meal'
                    onSaved={invalidatePlan}
                />
            </div>

            {isLoading && <LoadingState label='Loading next meals…' />}

            {!isLoading && (
                <div className='space-y-3'>
                    {entries.length === 0 ? (
                        <div className='rounded-xl border border-dashed border-border/70 bg-card/50 p-8 text-center text-muted-foreground'>
                            No next meals yet. Add one to start building your queue.
                        </div>
                    ) : (
                        entries.map((entry, index) => (
                            <motion.div
                                key={entry.id}
                                initial={{ opacity: 0, y: 8 }}
                                animate={{ opacity: 1, y: 0 }}
                                transition={{ duration: 0.25, delay: Math.min(index * 0.03, 0.2) }}
                                className='flex items-center gap-3 rounded-xl border border-border/60 bg-card p-4'
                            >
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <button
                                            type='button'
                                            className='rounded-full p-0.5 text-muted-foreground transition-colors hover:text-foreground'
                                            aria-label={entry.status === 'completed' ? 'Mark as not done' : 'Mark as done'}
                                            onClick={() => toggleCompleted.mutate(entry)}
                                            disabled={toggleCompleted.isPending}
                                        >
                                            {entry.status === 'completed' ? (
                                                <CheckCircle2 className='h-5 w-5 shrink-0 text-emerald-500' />
                                            ) : (
                                                <Circle className='h-5 w-5 shrink-0 text-muted-foreground' />
                                            )}
                                        </button>
                                    </TooltipTrigger>
                                    <TooltipContent>
                                        {entry.status === 'completed' ? 'Mark as not done' : 'Mark as done'}
                                    </TooltipContent>
                                </Tooltip>
                                <div className='min-w-0 flex-1'>
                                    <Link
                                        to={`/workspaces/${workspaceId}/recipe/${entry.recipeId}`}
                                        className='block truncate text-sm font-semibold text-foreground hover:text-primary'
                                    >
                                        {entry.recipeTitle}
                                    </Link>
                                    <p className='mt-0.5 text-xs text-muted-foreground'>
                                        {mealLabels[(entry.mealType as (typeof mealTypes)[number]) ?? 'dinner']} •{' '}
                                        {formatDateLabel(entry.plannedDate)}
                                    </p>
                                    {entry.completedAtUtc && (
                                        <p className='mt-0.5 text-xs text-muted-foreground'>
                                            Completed {new Date(entry.completedAtUtc).toLocaleDateString()}
                                        </p>
                                    )}
                                </div>
                                <MealPlanEntryDialog
                                    workspaceId={workspaceId}
                                    recipes={recipes}
                                    entry={entry}
                                    triggerLabel='Edit'
                                    onSaved={invalidatePlan}
                                    onDeleted={invalidatePlan}
                                />
                            </motion.div>
                        ))
                    )}
                </div>
            )}
        </div>
    );
}
