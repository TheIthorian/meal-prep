import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { motion, AnimatePresence } from 'framer-motion';
import { Check, ShoppingCart, Trash2 } from 'lucide-react';
import { mealPlanApi, recipesApi, shoppingListsApi } from '@/lib/api';
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
import { ShoppingListGeneratorDialog } from '@/components/shopping/ShoppingListGeneratorDialog';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import type { ShoppingList as ShoppingListModel, ShoppingListItem, ShoppingListListItem } from '@/models/meal-prep';
import { getShoppingListProgress } from '@/lib/meal-prep';

function formatShoppingListDate(iso: string | null | undefined) {
    if (!iso) return null;
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return null;
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

export default function ShoppingListPage() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();
    const [activeListId, setActiveListId] = useState<string | null>(null);
    const [listPendingDelete, setListPendingDelete] = useState<ShoppingListListItem | null>(null);

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
        queryKey: ['meal-plan', workspaceId, 'shop'],
        queryFn: () => mealPlanApi.getAll(workspaceId),
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

    const deleteShoppingList = useMutation({
        mutationFn: (shoppingListId: string) => shoppingListsApi.remove(workspaceId, shoppingListId),
        onSuccess: (_, deletedId) => {
            queryClient.invalidateQueries({ queryKey: ['shopping-lists', workspaceId] });
            queryClient.removeQueries({ queryKey: ['shopping-list', workspaceId, deletedId] });
            setActiveListId(current => (current === deletedId ? null : current));
            setListPendingDelete(null);
            capture(
                analyticsEvents.shoppingListDeleted,
                withWorkspaceProperties(currentWorkspace, { shopping_list_id: deletedId }),
            );
            toast({
                title: 'Shopping list deleted',
                description: 'The list was removed from your workspace.',
            });
        },
        onError: () => {
            toast({
                title: 'Could not delete list',
                description: 'Try again in a moment.',
                variant: 'destructive',
            });
        },
    });

    const isLoading = listsLoading || (Boolean(selectedId) && detailLoading);

    return (
        <div className='mx-auto max-w-6xl px-4 py-6 md:px-8 md:py-10'>
            <motion.div
                initial={{ opacity: 0, y: -8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3 }}
                className='mb-8'
            >
                <h1 className='font-heading text-3xl text-foreground md:text-4xl'>Shopping lists</h1>
                <p className='mt-1 text-muted-foreground'>
                    View every list in this workspace, switch between them, or remove ones you no longer need.
                </p>
            </motion.div>

            {!listsLoading && listSummaries.length === 0 && (
                <div className='space-y-6'>
                    <EmptyState
                        title='No shopping list yet'
                        description='Generate a list from your recipes and next meals.'
                    />
                    <div className='flex justify-center'>
                        <ShoppingListGeneratorDialog
                            workspaceId={workspaceId}
                            recipes={recipes}
                            entries={planEntries}
                            onGenerated={onGenerated}
                        />
                    </div>
                </div>
            )}

            {!listsLoading && listSummaries.length > 0 && (
                <div className='grid gap-8 lg:grid-cols-[minmax(260px,300px)_1fr] lg:items-start'>
                    <aside className='rounded-xl border border-border/60 bg-card p-4 shadow-sm'>
                        <h2 className='text-sm font-semibold text-foreground'>Your lists</h2>
                        <p className='mt-0.5 text-xs text-muted-foreground'>
                            {listSummaries.length} list{listSummaries.length === 1 ? '' : 's'}
                        </p>
                        <ul className='mt-4 space-y-2'>
                            {listSummaries.map(list => {
                                const pct = getShoppingListProgress(list);
                                const isSelected = list.id === selectedId;
                                const dateLabel = formatShoppingListDate(list.generatedAt);
                                return (
                                    <li key={list.id}>
                                        <div
                                            className={`flex gap-2 rounded-lg border p-3 transition-colors ${
                                                isSelected
                                                    ? 'border-primary/50 bg-primary/5'
                                                    : 'border-border/50 bg-background hover:border-border'
                                            }`}
                                        >
                                            <button
                                                type='button'
                                                onClick={() => setActiveListId(list.id)}
                                                className='min-w-0 flex-1 text-left'
                                            >
                                                <span className='block truncate text-sm font-medium text-foreground'>
                                                    {list.name}
                                                </span>
                                                <span className='mt-0.5 block text-xs text-muted-foreground'>
                                                    {dateLabel ? `${dateLabel} · ` : ''}
                                                    <span className='whitespace-nowrap tabular-nums'>
                                                        {list.checkedItemCount}/{list.totalItemCount} checked
                                                    </span>
                                                </span>
                                                <div className='mt-2 h-1 overflow-hidden rounded-full bg-secondary'>
                                                    <div
                                                        className='h-full rounded-full bg-primary transition-all'
                                                        style={{ width: `${pct}%` }}
                                                    />
                                                </div>
                                            </button>
                                            <button
                                                type='button'
                                                className='flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive'
                                                aria-label={`Delete ${list.name}`}
                                                onClick={e => {
                                                    e.stopPropagation();
                                                    setListPendingDelete(list);
                                                }}
                                            >
                                                <Trash2 className='h-4 w-4' />
                                            </button>
                                        </div>
                                    </li>
                                );
                            })}
                        </ul>
                    </aside>

                    <section className='min-w-0'>
                        <motion.div
                            initial={{ opacity: 0, y: -4 }}
                            animate={{ opacity: 1, y: 0 }}
                            transition={{ duration: 0.25 }}
                            className='mb-6'
                        >
                            {shoppingList && (
                                <>
                                    <h2 className='font-heading text-xl text-foreground md:text-2xl'>
                                        {shoppingList.name}
                                    </h2>
                                    <p className='mt-1 text-muted-foreground'>
                                        {checkedCount} of {total} items checked
                                    </p>
                                    <div className='mt-3 h-1.5 overflow-hidden rounded-full bg-secondary'>
                                        <motion.div
                                            className='h-full rounded-full bg-primary'
                                            initial={{ width: 0 }}
                                            animate={{ width: `${progress}%` }}
                                            transition={{ duration: 0.4, ease: 'easeOut' }}
                                        />
                                    </div>
                                </>
                            )}

                            <div className='mt-4 flex flex-wrap items-center gap-2'>
                                <ShoppingListGeneratorDialog
                                    workspaceId={workspaceId}
                                    recipes={recipes}
                                    entries={planEntries}
                                    onGenerated={onGenerated}
                                />
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
                                                        onUpdated={() => {
                                                            void queryClient.invalidateQueries({
                                                                queryKey: ['shopping-lists', workspaceId],
                                                            });
                                                            void queryClient.invalidateQueries({
                                                                queryKey: [
                                                                    'shopping-list',
                                                                    workspaceId,
                                                                    shoppingList.id,
                                                                ],
                                                            });
                                                        }}
                                                    />
                                                ))}
                                            </AnimatePresence>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </section>
                </div>
            )}

            <AlertDialog
                open={listPendingDelete !== null}
                onOpenChange={open => {
                    if (!open) setListPendingDelete(null);
                }}
            >
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Delete this shopping list?</AlertDialogTitle>
                        <AlertDialogDescription>
                            {listPendingDelete
                                ? `"${listPendingDelete.name}" will be removed. You can generate a new list anytime.`
                                : null}
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel disabled={deleteShoppingList.isPending}>Cancel</AlertDialogCancel>
                        <Button
                            variant='destructive'
                            disabled={deleteShoppingList.isPending || !listPendingDelete}
                            onClick={() => {
                                if (listPendingDelete) deleteShoppingList.mutate(listPendingDelete.id);
                            }}
                        >
                            {deleteShoppingList.isPending ? 'Deleting…' : 'Delete list'}
                        </Button>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
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
                sourceNames: item.sourceNames,
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
                <span className='ml-2 text-xs tabular-nums text-muted-foreground'>{item.displayText}</span>
                {item.sourceNames.length > 0 && (
                    <span
                        className={`mt-0.5 block text-xs ${item.isChecked ? 'text-muted-foreground/80' : 'text-muted-foreground'}`}
                    >
                        For {item.sourceNames.join(' · ')}
                    </span>
                )}
            </div>
        </motion.button>
    );
}
