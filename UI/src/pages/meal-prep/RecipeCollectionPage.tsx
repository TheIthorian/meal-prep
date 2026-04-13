import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Download, Share2, Trash2, X } from 'lucide-react';
import { recipeCollectionsApi } from '@/lib/api';
import { RecipeCard } from '@/components/meal-prep/RecipeCard';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
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
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';

function workspacePath(workspaceId: string, subPath: string) {
    const trimmed = subPath.replace(/^\//, '');
    return `/workspaces/${workspaceId}/${trimmed}`;
}

function slugifyDownloadName(name: string) {
    return name.replace(/[^\w\s-]/g, '').replace(/\s+/g, '-').slice(0, 80) || 'recipes';
}

export default function RecipeCollectionPage() {
    const { workspaceId = '', collectionId = '' } = useParams<{
        workspaceId: string;
        collectionId: string;
    }>();
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { workspaces, currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();
    const [deleteOpen, setDeleteOpen] = useState(false);
    const [shareTargetId, setShareTargetId] = useState<string>('');

    const { data: detail, isLoading } = useQuery({
        queryKey: ['recipe-collection', workspaceId, collectionId],
        queryFn: () => recipeCollectionsApi.get(workspaceId, collectionId),
        enabled: Boolean(workspaceId && collectionId),
    });

    const ownerWorkspaceId = detail?.ownerWorkspaceId ?? workspaceId;

    const shareOptions = useMemo(() => {
        if (!detail) return [];
        return workspaces.filter(
            w => w.workspaceId !== detail.ownerWorkspaceId && !detail.sharedWithWorkspaces.some(s => s.workspaceId === w.workspaceId),
        );
    }, [detail, workspaces]);

    const deleteCollection = useMutation({
        mutationFn: () => recipeCollectionsApi.remove(detail!.ownerWorkspaceId, collectionId),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            toast({ title: 'Collection deleted' });
            setDeleteOpen(false);
            navigate(workspacePath(workspaceId, '/'));
        },
    });

    const shareMutation = useMutation({
        mutationFn: (targetWorkspaceId: string) =>
            recipeCollectionsApi.share(detail!.ownerWorkspaceId, collectionId, targetWorkspaceId),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collection', workspaceId, collectionId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            setShareTargetId('');
            toast({ title: 'Collection shared' });
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeCollectionShared,
                    withWorkspaceProperties(currentWorkspace, { collection_id: collectionId }),
                );
            }
        },
    });

    const unshareMutation = useMutation({
        mutationFn: (targetWorkspaceId: string) =>
            recipeCollectionsApi.unshare(detail!.ownerWorkspaceId, collectionId, targetWorkspaceId),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collection', workspaceId, collectionId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            toast({ title: 'Sharing removed' });
        },
    });

    async function handleExport() {
        if (!detail) return;
        try {
            const data = await recipeCollectionsApi.exportJson(workspaceId, collectionId);
            const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `${slugifyDownloadName(data.collectionName)}-collection.json`;
            a.click();
            URL.revokeObjectURL(url);
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeCollectionExported,
                    withWorkspaceProperties(currentWorkspace, {
                        collection_id: collectionId,
                        recipe_count: data.recipes.length,
                    }),
                );
            }
            toast({ title: 'Download started' });
        } catch {
            toast({ title: 'Export failed', variant: 'destructive' });
        }
    }

    const removeRecipe = useMutation({
        mutationFn: (recipeId: string) =>
            recipeCollectionsApi.removeRecipe(detail!.ownerWorkspaceId, collectionId, recipeId),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collection', workspaceId, collectionId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            toast({ title: 'Removed from collection' });
        },
    });

    if (isLoading || !detail) {
        return (
            <div className='mx-auto max-w-6xl px-4 py-10 md:px-8'>
                <LoadingState label='Loading collection…' />
            </div>
        );
    }

    return (
        <div className='mx-auto max-w-6xl px-4 py-6 md:px-8 md:py-10'>
            <div className='mb-6'>
                <Link
                    to={workspacePath(workspaceId, '/')}
                    className='mb-4 inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground'
                >
                    <ArrowLeft className='h-4 w-4' />
                    Back to recipes
                </Link>
                <div className='flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between'>
                    <div>
                        <h1 className='font-heading text-3xl text-foreground md:text-4xl'>{detail.name}</h1>
                        {detail.description ? (
                            <p className='mt-2 text-muted-foreground'>{detail.description}</p>
                        ) : null}
                        {workspaceId !== detail.ownerWorkspaceId ? (
                            <p className='mt-2 text-sm text-muted-foreground'>
                                Shared into this workspace. Recipes open in their home workspace.
                            </p>
                        ) : null}
                    </div>
                    <div className='flex flex-wrap gap-2'>
                        <Button type='button' variant='outline' size='sm' className='gap-1.5' onClick={() => void handleExport()}>
                            <Download className='h-4 w-4' />
                            Download JSON
                        </Button>
                        {detail.canEdit ? (
                            <>
                                <Button
                                    type='button'
                                    variant='outline'
                                    size='sm'
                                    className='gap-1.5 text-destructive hover:bg-destructive/10'
                                    onClick={() => setDeleteOpen(true)}
                                >
                                    <Trash2 className='h-4 w-4' />
                                    Delete
                                </Button>
                            </>
                        ) : null}
                    </div>
                </div>
            </div>

            {detail.canEdit && (
                <div className='mb-8 rounded-lg border border-border bg-card p-4'>
                    <div className='mb-3 flex items-center gap-2 text-sm font-medium text-foreground'>
                        <Share2 className='h-4 w-4' />
                        Share with another workspace
                    </div>
                    <p className='mb-3 text-xs text-muted-foreground'>
                        Members of that workspace will see this collection in their sidebar. They still need access to
                        recipes in the owner workspace to open them.
                    </p>
                    <div className='flex flex-wrap items-end gap-2'>
                        <div className='min-w-[200px] flex-1'>
                            <Select value={shareTargetId} onValueChange={setShareTargetId}>
                                <SelectTrigger aria-label='Workspace to share with'>
                                    <SelectValue placeholder='Choose workspace…' />
                                </SelectTrigger>
                                <SelectContent>
                                    {shareOptions.map(w => (
                                        <SelectItem key={w.workspaceId} value={w.workspaceId}>
                                            {w.name}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <Button
                            type='button'
                            size='sm'
                            disabled={!shareTargetId || shareMutation.isPending}
                            onClick={() => void shareMutation.mutateAsync(shareTargetId)}
                        >
                            Share
                        </Button>
                    </div>
                    {detail.sharedWithWorkspaces.length > 0 && (
                        <ul className='mt-4 flex flex-wrap gap-2'>
                            {detail.sharedWithWorkspaces.map(sw => (
                                <li
                                    key={sw.workspaceId}
                                    className='flex items-center gap-1 rounded-full bg-secondary px-3 py-1 text-xs'
                                >
                                    <span>{sw.workspaceName}</span>
                                    <button
                                        type='button'
                                        className='rounded p-0.5 text-muted-foreground hover:bg-background hover:text-foreground'
                                        aria-label={`Stop sharing with ${sw.workspaceName}`}
                                        onClick={() => void unshareMutation.mutateAsync(sw.workspaceId)}
                                    >
                                        <X className='h-3.5 w-3.5' />
                                    </button>
                                </li>
                            ))}
                        </ul>
                    )}
                </div>
            )}

            {detail.recipes.length === 0 && (
                <EmptyState
                    title='No recipes in this collection'
                    description='Add recipes from the library or recipe page using “Add to collection”.'
                />
            )}

            {detail.recipes.length > 0 && (
                <div className='grid gap-6 sm:grid-cols-2 lg:grid-cols-3'>
                    {detail.recipes.map((recipe, index) => (
                        <div key={recipe.id} className='flex flex-col gap-2 sm:flex-row sm:items-stretch'>
                            <div className='min-w-0 flex-1'>
                                <RecipeCard workspaceId={ownerWorkspaceId} recipe={recipe} index={index} />
                            </div>
                            {detail.canEdit ? (
                                <Button
                                    type='button'
                                    variant='outline'
                                    size='sm'
                                    className='shrink-0 self-start sm:mt-2 sm:self-start'
                                    onClick={() => void removeRecipe.mutateAsync(recipe.id)}
                                    disabled={removeRecipe.isPending}
                                >
                                    Remove
                                </Button>
                            ) : null}
                        </div>
                    ))}
                </div>
            )}

            <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Delete this collection?</AlertDialogTitle>
                        <AlertDialogDescription>
                            Recipes stay in your library; only the grouping is removed. Sharing links will stop working.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <Button
                            variant='destructive'
                            disabled={deleteCollection.isPending}
                            onClick={() => void deleteCollection.mutateAsync()}
                        >
                            Delete
                        </Button>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </div>
    );
}
