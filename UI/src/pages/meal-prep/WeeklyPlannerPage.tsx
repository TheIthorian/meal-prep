import { useMemo } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { motion } from 'framer-motion';
import { Plus } from 'lucide-react';
import { mealPlanApi, recipesApi } from '@/lib/api';
import { addDays, startOfWeek, toDateInputValue } from '@/lib/meal-prep';
import type { MealPlanEntry } from '@/models/meal-prep';
import { MealPlanEntryDialog } from '@/components/planner/MealPlanEntryDialog';
import { LoadingState } from '@/components/common/LoadingState';

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

    const weekRange = useMemo(() => {
        const start = startOfWeek(new Date());
        const end = addDays(start, 6);
        return { from: toDateInputValue(start), to: toDateInputValue(end) };
    }, []);

    const { data: entries = [], isLoading: entriesLoading } = useQuery({
        queryKey: ['meal-plan', workspaceId, weekRange.from, weekRange.to],
        queryFn: () => mealPlanApi.getAll(workspaceId, { from: weekRange.from, to: weekRange.to }),
        enabled: Boolean(workspaceId),
    });

    const { data: recipesPage, isLoading: recipesLoading } = useQuery({
        queryKey: ['recipes', workspaceId, 'planner-select'],
        queryFn: () => recipesApi.getAll(workspaceId, { page: 1, pageSize: 100, includeArchived: false }),
        enabled: Boolean(workspaceId),
    });

    const recipes = recipesPage?.data ?? [];

    const entryMap = useMemo(() => {
        const map: Record<string, MealPlanEntry> = {};
        for (const entry of entries) {
            map[`${entry.plannedDate}|${entry.mealType}`] = entry;
        }
        return map;
    }, [entries]);

    const days = useMemo(() => {
        const start = startOfWeek(new Date());
        return Array.from({ length: 7 }, (_, i) => {
            const d = addDays(start, i);
            return {
                date: d,
                dateStr: toDateInputValue(d),
                label: d.toLocaleDateString(undefined, { weekday: 'long' }),
                short: d.toLocaleDateString(undefined, { weekday: 'short' }),
            };
        });
    }, []);

    const todayStr = toDateInputValue(new Date());

    function invalidatePlan() {
        queryClient.invalidateQueries({ queryKey: ['meal-plan', workspaceId] });
    }

    const isLoading = entriesLoading || recipesLoading;

    return (
        <div className='mx-auto max-w-6xl px-4 py-6 md:px-8 md:py-10'>
            <motion.div
                initial={{ opacity: 0, y: -8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3 }}
                className='mb-8'
            >
                <h1 className='font-heading text-3xl text-foreground md:text-4xl'>Weekly planner</h1>
                <p className='mt-1 text-muted-foreground'>Plan your meals for the week ahead</p>
            </motion.div>

            {isLoading && <LoadingState label='Loading planner…' />}

            {!isLoading && (
                <>
                    <div className='hidden overflow-x-auto md:block'>
                        <div className='grid min-w-[960px] grid-cols-7 gap-3'>
                            {days.map(day => (
                                <div key={day.dateStr} className='space-y-2'>
                                    <div
                                        className={`rounded-lg py-2 text-center text-sm font-medium ${
                                            day.dateStr === todayStr
                                                ? 'bg-primary/10 text-primary'
                                                : 'text-muted-foreground'
                                        }`}
                                    >
                                        {day.short}
                                        {day.dateStr === todayStr && (
                                            <span className='mt-0.5 block text-[10px] uppercase tracking-wider'>
                                                Today
                                            </span>
                                        )}
                                    </div>
                                    {mealTypes.map(mt => {
                                        const entry = entryMap[`${day.dateStr}|${mt}`];
                                        return (
                                            <motion.div
                                                key={mt}
                                                initial={{ opacity: 0, scale: 0.95 }}
                                                animate={{ opacity: 1, scale: 1 }}
                                                transition={{ duration: 0.25 }}
                                                className={`min-h-[72px] rounded-lg border p-2.5 transition-colors ${
                                                    entry
                                                        ? 'border-border/50 bg-card hover:border-primary/20'
                                                        : 'border-dashed border-border/60 hover:border-primary/30 hover:bg-primary/[0.02]'
                                                }`}
                                            >
                                                <p className='mb-1 text-[10px] uppercase tracking-wider text-muted-foreground/60'>
                                                    {mealLabels[mt]}
                                                </p>
                                                {entry ? (
                                                    <Link
                                                        to={`/workspaces/${workspaceId}/recipe/${entry.recipeId}`}
                                                        className='block'
                                                    >
                                                        <p className='line-clamp-3 text-xs font-medium leading-snug text-foreground'>
                                                            {entry.recipeTitle}
                                                        </p>
                                                    </Link>
                                                ) : recipes.length > 0 ? (
                                                    <MealPlanEntryDialog
                                                        workspaceId={workspaceId}
                                                        recipes={recipes}
                                                        selectedDate={day.dateStr}
                                                        defaultMealType={mt}
                                                        triggerLabel=''
                                                        onSaved={invalidatePlan}
                                                    />
                                                ) : (
                                                    <button
                                                        type='button'
                                                        className='flex h-8 w-full items-center justify-center text-muted-foreground/40'
                                                        disabled
                                                    >
                                                        <Plus className='h-4 w-4' />
                                                    </button>
                                                )}
                                            </motion.div>
                                        );
                                    })}
                                </div>
                            ))}
                        </div>
                    </div>

                    <div className='space-y-4 md:hidden'>
                        {days.map(day => (
                            <motion.div
                                key={day.dateStr}
                                initial={{ opacity: 0, y: 12 }}
                                animate={{ opacity: 1, y: 0 }}
                                transition={{ duration: 0.3 }}
                            >
                                <div
                                    className={`mb-2 flex items-center gap-2 ${
                                        day.dateStr === todayStr ? 'text-primary' : 'text-foreground'
                                    }`}
                                >
                                    <h3 className='font-heading text-lg'>{day.label}</h3>
                                    {day.dateStr === todayStr && (
                                        <span className='rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-primary'>
                                            Today
                                        </span>
                                    )}
                                </div>
                                <div className='space-y-2'>
                                    {mealTypes.map(mt => {
                                        const entry = entryMap[`${day.dateStr}|${mt}`];
                                        return (
                                            <div
                                                key={mt}
                                                className={`flex items-center gap-3 rounded-lg border p-3 transition-colors ${
                                                    entry
                                                        ? 'border-border/50 bg-card'
                                                        : 'border-dashed border-border/60'
                                                }`}
                                            >
                                                <span className='w-16 flex-shrink-0 text-xs text-muted-foreground/60'>
                                                    {mealLabels[mt]}
                                                </span>
                                                {entry ? (
                                                    <Link
                                                        to={`/workspaces/${workspaceId}/recipe/${entry.recipeId}`}
                                                        className='min-w-0 flex-1 truncate text-sm font-medium text-foreground'
                                                    >
                                                        {entry.recipeTitle}
                                                    </Link>
                                                ) : recipes.length > 0 ? (
                                                    <MealPlanEntryDialog
                                                        workspaceId={workspaceId}
                                                        recipes={recipes}
                                                        selectedDate={day.dateStr}
                                                        defaultMealType={mt}
                                                        triggerLabel='Add meal'
                                                        onSaved={invalidatePlan}
                                                    />
                                                ) : (
                                                    <span className='text-xs text-muted-foreground'>
                                                        Add recipes first
                                                    </span>
                                                )}
                                            </div>
                                        );
                                    })}
                                </div>
                            </motion.div>
                        ))}
                    </div>
                </>
            )}
        </div>
    );
}
