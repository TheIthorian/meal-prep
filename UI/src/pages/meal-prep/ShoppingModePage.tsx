import { useEffect, useMemo, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { motion, AnimatePresence } from 'framer-motion';
import { Check, ArrowLeft, Eye, EyeOff } from 'lucide-react';
import { shoppingListsApi } from '@/lib/api';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import type { ShoppingListItem } from '@/models/meal-prep';
import { LoadingState } from '@/components/common/LoadingState';

export default function ShoppingModePage() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const { setCurrentWorkspaceId } = useWorkspace();
    const queryClient = useQueryClient();
    const [searchParams] = useSearchParams();
    const listId = searchParams.get('list');
    const [hideCompleted, setHideCompleted] = useState(false);
    const [pendingId, setPendingId] = useState<string | null>(null);

    useEffect(() => {
        if (workspaceId) {
            try {
                setCurrentWorkspaceId(workspaceId);
            } catch {
                /* invalid workspace — ProtectedRoute still gates auth */
            }
        }
    }, [setCurrentWorkspaceId, workspaceId]);

    const { data: shoppingList, isLoading } = useQuery({
        queryKey: ['shopping-list', workspaceId, listId],
        queryFn: () => shoppingListsApi.getById(workspaceId, listId!),
        enabled: Boolean(workspaceId && listId),
    });

    const items = useMemo(() => shoppingList?.items ?? [], [shoppingList?.items]);

    const visibleItems = useMemo(
        () => (hideCompleted ? items.filter(i => !i.isChecked) : items),
        [hideCompleted, items],
    );
    const groupedVisibleItems = useMemo(() => {
        const groups: Record<string, ShoppingListItem[]> = {};
        for (const item of visibleItems) {
            const category = item.category?.trim() || 'Other';
            (groups[category] ??= []).push(item);
        }
        return Object.entries(groups).sort(([left], [right]) => left.localeCompare(right));
    }, [visibleItems]);

    const checkedCount = items.filter(i => i.isChecked).length;
    const progress = items.length > 0 ? (checkedCount / items.length) * 100 : 0;

    async function toggle(item: ShoppingListItem) {
        if (!shoppingList) return;
        setPendingId(item.id);
        try {
            await shoppingListsApi.updateItem(workspaceId, shoppingList.id, item.id, {
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
            await queryClient.invalidateQueries({ queryKey: ['shopping-list', workspaceId, listId] });
        } finally {
            setPendingId(null);
        }
    }

    if (!listId) {
        return (
            <div className='flex min-h-screen flex-col items-center justify-center bg-background px-4'>
                <p className='text-muted-foreground'>No list selected.</p>
                <Link to={`/workspaces/${workspaceId}/shopping`} className='mt-2 text-sm text-primary'>
                    Back to shopping list
                </Link>
            </div>
        );
    }

    if (isLoading || !shoppingList) {
        return (
            <div className='flex min-h-screen items-center justify-center bg-background'>
                <LoadingState label='Loading list…' />
            </div>
        );
    }

    return (
        <div className='flex min-h-screen flex-col bg-background'>
            <div
                className='sticky top-0 z-20 border-b border-border bg-background/95 px-4 pb-3 backdrop-blur-md'
                style={{ paddingTop: 'calc(env(safe-area-inset-top, 0px) + 1rem)' }}
            >
                <div className='mx-auto max-w-lg'>
                    <div className='mb-2 flex items-center justify-between'>
                        <Link
                            to={`/workspaces/${workspaceId}/shopping`}
                            className='flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground'
                        >
                            <ArrowLeft className='h-4 w-4' />
                            Exit
                        </Link>
                        <span className='text-sm font-medium tabular-nums text-foreground'>
                            {checkedCount}/{items.length}
                        </span>
                        <button
                            type='button'
                            onClick={() => setHideCompleted(!hideCompleted)}
                            className='flex items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground'
                        >
                            {hideCompleted ? <Eye className='h-4 w-4' /> : <EyeOff className='h-4 w-4' />}
                        </button>
                    </div>
                    <div className='h-2 overflow-hidden rounded-full bg-secondary'>
                        <motion.div
                            className='h-full rounded-full bg-primary'
                            animate={{ width: `${progress}%` }}
                            transition={{ duration: 0.3, ease: 'easeOut' }}
                        />
                    </div>
                </div>
            </div>

            <div className='mx-auto w-full max-w-lg flex-1 px-4 py-4'>
                <AnimatePresence mode='popLayout'>
                    {groupedVisibleItems.map(([category, categoryItems]) => (
                        <motion.section
                            key={category}
                            layout
                            initial={{ opacity: 0, y: 4 }}
                            animate={{ opacity: 1, y: 0 }}
                            exit={{ opacity: 0, y: -4 }}
                            transition={{ duration: 0.2 }}
                            className='mb-5'
                        >
                            <h3 className='mb-2 text-xs font-medium uppercase tracking-wider text-muted-foreground'>
                                {category}
                            </h3>
                            <div>
                                {categoryItems.map(item => (
                                    <motion.button
                                        key={item.id}
                                        type='button'
                                        layout
                                        disabled={pendingId === item.id}
                                        initial={{ opacity: 0, scale: 0.95 }}
                                        animate={{ opacity: 1, scale: 1 }}
                                        exit={{ opacity: 0, scale: 0.9 }}
                                        transition={{ duration: 0.2 }}
                                        onClick={() => void toggle(item)}
                                        className={`mb-2 flex w-full items-center gap-4 rounded-xl border p-4 text-left transition-all active:scale-[0.98] ${
                                            item.isChecked
                                                ? 'border-border/30 bg-muted/40'
                                                : 'border-border/50 bg-card active:bg-primary/5'
                                        }`}
                                    >
                                        <div
                                            className={`flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full border-2 transition-all ${
                                                item.isChecked
                                                    ? 'animate-scale-check border-primary bg-primary'
                                                    : 'border-muted-foreground/30'
                                            }`}
                                        >
                                            {item.isChecked && <Check className='h-4 w-4 text-primary-foreground' />}
                                        </div>
                                        <div className='min-w-0 flex-1'>
                                            <span
                                                className={`text-base font-medium ${
                                                    item.isChecked
                                                        ? 'text-muted-foreground line-through'
                                                        : 'text-foreground'
                                                }`}
                                            >
                                                {item.name}
                                            </span>
                                            <span className='ml-2 text-sm tabular-nums text-muted-foreground'>
                                                {item.displayText}
                                            </span>
                                            {item.sourceNames.length > 0 && (
                                                <span
                                                    className={`mt-1 block text-sm ${
                                                        item.isChecked
                                                            ? 'text-muted-foreground/80'
                                                            : 'text-muted-foreground'
                                                    }`}
                                                >
                                                    For {item.sourceNames.join(' · ')}
                                                </span>
                                            )}
                                        </div>
                                    </motion.button>
                                ))}
                            </div>
                        </motion.section>
                    ))}
                </AnimatePresence>

                {visibleItems.length === 0 && (
                    <div className='py-16 text-center'>
                        <p className='font-heading mb-2 text-2xl text-foreground'>All done!</p>
                        <p className='text-muted-foreground'>Every item has been checked off.</p>
                    </div>
                )}
            </div>
        </div>
    );
}
