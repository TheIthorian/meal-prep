import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { motion, AnimatePresence } from 'framer-motion';
import { Check, ShoppingCart } from 'lucide-react';
import { mealPlanApi, recipesApi, shoppingListsApi } from '@/lib/api';
import { ShoppingListGeneratorDialog } from '@/components/shopping/ShoppingListGeneratorDialog';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import type { ShoppingList as ShoppingListModel, ShoppingListItem } from '@/models/meal-prep';
import { startOfWeek, toDateInputValue, addDays } from '@/lib/meal-prep';

export default function ShoppingListPage() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const queryClient = useQueryClient();
    const [activeListId, setActiveListId] = useState<string | null>(null);

    const weekRange = useMemo(() => {
        const start = startOfWeek(new Date());
        const end = addDays(start, 6);
        return { from: toDateInputValue(start), to: toDateInputValue(end) };
    }, []);

    const { data: listSummaries = [], isLoading: listsLoading } = useQuery({
        queryKey: ['shopping-lists', workspaceId],
        queryFn: () => shoppingListsApi.getAll(workspaceId),
        enabled: Boolean(workspaceId),
    });

    const selectedId = activeListId ?? listSummaries[0]?.id ?? null;

    const { data: shoppingList, isLoading: detailLoading } = useQuery({
        queryKey: ['shopping-list', workspaceId, selectedId],
        queryFn: () => shoppingListsApi.getById(workspaceId, selectedId!),
        enabled: Boolean(workspaceId && selectedId),
    });

    const { data: recipesPage } = useQuery({
        queryKey: ['recipes', workspaceId, 'shop-gen'],
        queryFn: () => recipesApi.getAll(workspaceId, { page: 1, pageSize: 100, includeArchived: false }),
        enabled: Boolean(workspaceId),
    });

    const { data: planEntries = [] } = useQuery({
        queryKey: ['meal-plan', workspaceId, weekRange.from, weekRange.to, 'shop'],
        queryFn: () => mealPlanApi.getAll(workspaceId, { from: weekRange.from, to: weekRange.to }),
        enabled: Boolean(workspaceId),
    });

    const recipes = recipesPage?.data ?? [];

    const grouped = useMemo(() => {
        if (!shoppingList) return [] as [string, ShoppingListItem[]][];
        const groups: Record<string, ShoppingListItem[]> = {};
        for (const item of shoppingList.items) {
            const cat = item.category?.trim() || 'Other';
            (groups[cat] ??= []).push(item);
        }
        return Object.entries(groups).sort(([a], [b]) => a.localeCompare(b));
    }, [shoppingList]);

    const checkedCount = shoppingList?.items.filter(i => i.isChecked).length ?? 0;
    const total = shoppingList?.items.length ?? 0;
    const progress = total > 0 ? (checkedCount / total) * 100 : 0;

    function onGenerated(list: ShoppingListModel) {
        queryClient.invalidateQueries({ queryKey: ['shopping-lists', workspaceId] });
        setActiveListId(list.id);
    }

    const isLoading = listsLoading || (Boolean(selectedId) && detailLoading);

    return (
        <div className='mx-auto max-w-2xl px-4 py-6 md:px-8 md:py-10'>
            <motion.div
                initial={{ opacity: 0, y: -8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3 }}
                className='mb-8'
            >
                <h1 className='font-heading text-3xl text-foreground md:text-4xl'>Shopping list</h1>
                <p className='mt-1 text-muted-foreground'>
                    {shoppingList ? `${checkedCount} of ${total} items checked` : 'Generate or pick a list'}
                </p>

                <div className='mt-3 h-1.5 overflow-hidden rounded-full bg-secondary'>
                    <motion.div
                        className='h-full rounded-full bg-primary'
                        initial={{ width: 0 }}
                        animate={{ width: `${progress}%` }}
                        transition={{ duration: 0.4, ease: 'easeOut' }}
                    />
                </div>

                <div className='mt-4 flex flex-wrap items-center gap-2'>
                    <ShoppingListGeneratorDialog
                        workspaceId={workspaceId}
                        recipes={recipes}
                        entries={planEntries}
                        onGenerated={onGenerated}
                    />
                    {listSummaries.length > 1 && (
                        <select
                            className='rounded-lg border border-border bg-card px-3 py-2 text-sm'
                            value={selectedId ?? ''}
                            onChange={e => setActiveListId(e.target.value || null)}
                        >
                            {listSummaries.map(list => (
                                <option key={list.id} value={list.id}>
                                    {list.name}
                                </option>
                            ))}
                        </select>
                    )}
                    {shoppingList && (
                        <Link
                            to={`/workspaces/${workspaceId}/shopping-mode?list=${shoppingList.id}`}
                            className='inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2.5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90'
                        >
                            <ShoppingCart className='h-4 w-4' />
                            Start shopping
                        </Link>
                    )}
                </div>
            </motion.div>

            {isLoading && <LoadingState label='Loading shopping list…' />}

            {!isLoading && !shoppingList && (
                <EmptyState
                    title='No shopping list yet'
                    description='Generate a list from your recipes and planned meals.'
                />
            )}

            {!isLoading && shoppingList && (
                <div className='space-y-6'>
                    {grouped.map(([category, categoryItems]) => (
                        <div key={category}>
                            <h3 className='mb-2 text-xs font-medium uppercase tracking-wider text-muted-foreground'>
                                {category}
                            </h3>
                            <div className='space-y-1'>
                                <AnimatePresence>
                                    {categoryItems.map(item => (
                                        <ShoppingListRow
                                            key={item.id}
                                            workspaceId={workspaceId}
                                            shoppingListId={shoppingList.id}
                                            item={item}
                                            onUpdated={() =>
                                                queryClient.invalidateQueries({
                                                    queryKey: ['shopping-list', workspaceId, shoppingList.id],
                                                })
                                            }
                                        />
                                    ))}
                                </AnimatePresence>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

function ShoppingListRow({
    workspaceId,
    shoppingListId,
    item,
    onUpdated,
}: {
    workspaceId: string;
    shoppingListId: string;
    item: ShoppingListItem;
    onUpdated: () => void;
}) {
    const [pending, setPending] = useState(false);

    async function toggle() {
        setPending(true);
        try {
            await shoppingListsApi.updateItem(workspaceId, shoppingListId, item.id, {
                name: item.name,
                normalizedIngredientName: item.normalizedIngredientName,
                amount: item.amount,
                unit: item.unit,
                isApproximate: item.isApproximate,
                isChecked: !item.isChecked,
                isManual: item.isManual,
                category: item.category,
                note: item.note,
                displayText: item.displayText,
            });
            onUpdated();
        } finally {
            setPending(false);
        }
    }

    return (
        <motion.button
            type='button'
            layout
            disabled={pending}
            onClick={() => void toggle()}
            className={`flex w-full items-center gap-3 rounded-lg border p-3 text-left transition-all ${
                item.isChecked ? 'border-border/30 bg-muted/50' : 'border-border/50 bg-card hover:border-primary/20'
            }`}
        >
            <div
                className={`flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-full border-2 transition-colors ${
                    item.isChecked ? 'border-primary bg-primary' : 'border-border'
                }`}
            >
                {item.isChecked && <Check className='h-3 w-3 text-primary-foreground' />}
            </div>
            <div className='min-w-0 flex-1'>
                <span
                    className={`text-sm ${item.isChecked ? 'text-muted-foreground line-through' : 'text-foreground'}`}
                >
                    {item.name}
                </span>
                <span className='ml-2 text-xs tabular-nums text-muted-foreground'>
                    {item.displayText}
                </span>
            </div>
        </motion.button>
    );
}
