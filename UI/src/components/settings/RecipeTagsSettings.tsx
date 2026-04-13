import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Tags, Trash2 } from 'lucide-react';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { recipesApi } from '@/lib/api';
import { formatRecipeTagLabel } from '@/lib/meal-prep';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { toast } from '@/hooks/use-toast';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
    AlertDialog,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from '@/components/ui/alert-dialog';

export function RecipeTagsSettings() {
    const { currentWorkspace } = useWorkspace();
    const workspaceId = currentWorkspace?.workspaceId ?? '';
    const queryClient = useQueryClient();
    const { capture } = useAnalytics();

    const [singletonsConfirmOpen, setSingletonsConfirmOpen] = useState(false);
    const [selectedConfirmOpen, setSelectedConfirmOpen] = useState(false);
    const [onlySingletons, setOnlySingletons] = useState(false);
    const [selected, setSelected] = useState<Set<string>>(() => new Set());

    const { data, isLoading } = useQuery({
        queryKey: ['recipe-tag-usage', workspaceId],
        queryFn: () => recipesApi.getRecipeTagUsage(workspaceId),
        enabled: Boolean(workspaceId),
    });

    const items = data?.items ?? [];

    const visibleItems = useMemo(() => {
        if (!onlySingletons) return items;
        return items.filter(row => row.recipeCount === 1);
    }, [items, onlySingletons]);

    const singletonCount = useMemo(() => items.filter(row => row.recipeCount === 1).length, [items]);

    const bulkRemove = useMutation({
        mutationFn: (tags: string[]) => recipesApi.bulkRemoveRecipeTags(workspaceId, tags),
        onSuccess: (result, tags) => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-tag-usage', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-tags', workspaceId] });
            toast({
                title: 'Tags removed',
                description: `Updated ${result.recipesUpdated} recipe(s); cleared ${tags.length} tag value(s).`,
            });
            setSelected(new Set());
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeTagsBulkRemoved,
                    withWorkspaceProperties(currentWorkspace, {
                        recipes_updated: result.recipesUpdated,
                        tags_removed_count: tags.length,
                        removal_mode: 'selected',
                    }),
                );
            }
        },
        onError: () => {
            toast({
                title: 'Could not remove tags',
                description: 'Try again or check your connection.',
                variant: 'destructive',
            });
        },
    });

    const removeSingletons = useMutation({
        mutationFn: () => recipesApi.removeSingletonRecipeTags(workspaceId),
        onSuccess: result => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-tag-usage', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-tags', workspaceId] });
            toast({
                title: 'Single-recipe tags removed',
                description: `Updated ${result.recipesUpdated} recipe(s); dropped ${result.tagsProcessed.length} tag value(s) that only appeared once.`,
            });
            setSingletonsConfirmOpen(false);
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeTagsBulkRemoved,
                    withWorkspaceProperties(currentWorkspace, {
                        recipes_updated: result.recipesUpdated,
                        tags_removed_count: result.tagsProcessed.length,
                        removal_mode: 'singletons',
                    }),
                );
            }
        },
        onError: () => {
            toast({
                title: 'Could not remove tags',
                description: 'Try again or check your connection.',
                variant: 'destructive',
            });
        },
    });

    function toggleSelected(tag: string) {
        setSelected(prev => {
            const next = new Set(prev);
            if (next.has(tag)) next.delete(tag);
            else next.add(tag);
            return next;
        });
    }

    function toggleSelectAllVisible() {
        const visibleTags = visibleItems.map(row => row.tag);
        const allSelected = visibleTags.length > 0 && visibleTags.every(t => selected.has(t));
        if (allSelected) {
            setSelected(prev => {
                const next = new Set(prev);
                for (const t of visibleTags) next.delete(t);
                return next;
            });
        } else {
            setSelected(prev => {
                const next = new Set(prev);
                for (const t of visibleTags) next.add(t);
                return next;
            });
        }
    }

    if (!workspaceId) {
        return (
            <Card>
                <CardHeader>
                    <CardTitle className='flex items-center gap-2'>
                        <Tags className='h-5 w-5' aria-hidden />
                        Recipe tags
                    </CardTitle>
                    <CardDescription>Choose a workspace in the header to manage tags.</CardDescription>
                </CardHeader>
            </Card>
        );
    }

    const selectedList = [...selected];
    const visibleTagKeys = visibleItems.map(row => row.tag);
    const allVisibleSelected =
        visibleTagKeys.length > 0 && visibleTagKeys.every(t => selected.has(t));

    return (
        <>
            <Card>
                <CardHeader>
                    <CardTitle className='flex items-center gap-2'>
                        <Tags className='h-5 w-5' aria-hidden />
                        Recipe tags
                    </CardTitle>
                    <CardDescription>
                        Tags are stored exactly as on each recipe (including legacy imports). Remove stragglers from every
                        recipe that uses them, or clear all tags that only appear on a single recipe.
                    </CardDescription>
                </CardHeader>
                <CardContent className='space-y-4'>
                    <div className='flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between'>
                        <div className='flex items-center gap-2'>
                            <Checkbox
                                id='only-singletons'
                                checked={onlySingletons}
                                onCheckedChange={checked => setOnlySingletons(checked === true)}
                            />
                            <Label htmlFor='only-singletons' className='text-sm font-normal'>
                                Show only tags on one recipe ({singletonCount})
                            </Label>
                        </div>
                        <div className='flex flex-wrap gap-2'>
                            <Button
                                type='button'
                                variant='secondary'
                                disabled={singletonCount === 0 || removeSingletons.isPending}
                                onClick={() => setSingletonsConfirmOpen(true)}
                            >
                                Remove all single-recipe tags
                            </Button>
                            <Button
                                type='button'
                                variant='destructive'
                                disabled={selectedList.length === 0 || bulkRemove.isPending}
                                onClick={() => setSelectedConfirmOpen(true)}
                            >
                                <Trash2 className='mr-2 h-4 w-4' aria-hidden />
                                Remove selected ({selectedList.length})
                            </Button>
                        </div>
                    </div>

                    {isLoading && <p className='text-sm text-muted-foreground'>Loading tag usage…</p>}

                    {!isLoading && items.length === 0 && (
                        <p className='text-sm text-muted-foreground'>No tags on any recipe in this workspace.</p>
                    )}

                    {!isLoading && items.length > 0 && (
                        <div className='rounded-md border border-border'>
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead className='w-10'>
                                            <Checkbox
                                                checked={allVisibleSelected}
                                                onCheckedChange={() => toggleSelectAllVisible()}
                                                aria-label='Select all visible tags'
                                            />
                                        </TableHead>
                                        <TableHead>Tag</TableHead>
                                        <TableHead className='w-28 text-right'>Recipes</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {visibleItems.map(row => (
                                        <TableRow key={row.tag}>
                                            <TableCell>
                                                <Checkbox
                                                    checked={selected.has(row.tag)}
                                                    onCheckedChange={() => toggleSelected(row.tag)}
                                                    aria-label={`Select tag ${row.tag}`}
                                                />
                                            </TableCell>
                                            <TableCell>
                                                <span className='font-medium'>{formatRecipeTagLabel(row.tag)}</span>
                                                <span className='mt-0.5 block font-mono text-xs text-muted-foreground'>{row.tag}</span>
                                            </TableCell>
                                            <TableCell className='text-right tabular-nums'>{row.recipeCount}</TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </div>
                    )}
                </CardContent>
            </Card>

            <AlertDialog open={singletonsConfirmOpen} onOpenChange={setSingletonsConfirmOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Remove all single-recipe tags?</AlertDialogTitle>
                        <AlertDialogDescription>
                            This removes {singletonCount} distinct tag value{singletonCount === 1 ? '' : 's'} from the one
                            recipe each is attached to. It cannot be undone (you can re-add tags when editing a recipe).
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <Button
                            variant='destructive'
                            disabled={removeSingletons.isPending}
                            onClick={() => void removeSingletons.mutateAsync()}
                        >
                            {removeSingletons.isPending ? 'Removing…' : 'Remove'}
                        </Button>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

            <AlertDialog open={selectedConfirmOpen} onOpenChange={setSelectedConfirmOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Remove selected tags?</AlertDialogTitle>
                        <AlertDialogDescription>
                            These tag values will be stripped from every recipe that uses them ({selectedList.length}{' '}
                            value{selectedList.length === 1 ? '' : 's'}).
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <Button
                            variant='destructive'
                            disabled={bulkRemove.isPending}
                            onClick={() => {
                                setSelectedConfirmOpen(false);
                                void bulkRemove.mutateAsync(selectedList);
                            }}
                        >
                            {bulkRemove.isPending ? 'Removing…' : 'Remove'}
                        </Button>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </>
    );
}
