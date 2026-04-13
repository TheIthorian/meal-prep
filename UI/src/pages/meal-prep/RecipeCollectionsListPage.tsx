import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ChevronRight, FolderOpen, Plus } from 'lucide-react';
import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { recipeCollectionsApi } from '@/lib/api';
import { Button } from '@/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';

function workspacePath(workspaceId: string, subPath: string) {
    const trimmed = subPath.replace(/^\//, '');
    return `/workspaces/${workspaceId}/${trimmed}`;
}

export default function RecipeCollectionsListPage() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();
    const [createOpen, setCreateOpen] = useState(false);
    const [newName, setNewName] = useState('');

    const { data: collections = [], isLoading } = useQuery({
        queryKey: ['recipe-collections', workspaceId],
        queryFn: () => recipeCollectionsApi.list(workspaceId),
        enabled: Boolean(workspaceId),
    });

    const createCollection = useMutation({
        mutationFn: (name: string) => recipeCollectionsApi.create(workspaceId, { name }),
        onSuccess: data => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            setCreateOpen(false);
            setNewName('');
            toast({ title: 'Collection created' });
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeCollectionCreated,
                    withWorkspaceProperties(currentWorkspace, { collection_id: data.id }),
                );
            }
        },
    });

    function handleCreateSubmit(e: React.FormEvent) {
        e.preventDefault();
        const name = newName.trim();
        if (!name) return;
        void createCollection.mutateAsync(name);
    }

    return (
        <div className='mx-auto max-w-lg px-4 py-6 md:px-8 md:py-10'>
            <div className='mb-6 flex items-start justify-between gap-4'>
                <div>
                    <h1 className='font-heading text-2xl text-foreground md:text-3xl'>Collections</h1>
                    <p className='mt-1 text-sm text-muted-foreground'>
                        Group recipes, share with another workspace, or export a bundle.
                    </p>
                </div>
                <Dialog open={createOpen} onOpenChange={setCreateOpen}>
                    <DialogTrigger asChild>
                        <Button type='button' size='sm' className='shrink-0 gap-1'>
                            <Plus className='h-4 w-4' />
                            New
                        </Button>
                    </DialogTrigger>
                    <DialogContent>
                        <form onSubmit={handleCreateSubmit}>
                            <DialogHeader>
                                <DialogTitle>New collection</DialogTitle>
                            </DialogHeader>
                            <div className='py-4'>
                                <Label htmlFor='m-collection-name'>Name</Label>
                                <Input
                                    id='m-collection-name'
                                    value={newName}
                                    onChange={e => setNewName(e.target.value)}
                                    placeholder='e.g. Weeknight dinners'
                                    className='mt-2'
                                    autoFocus
                                />
                            </div>
                            <DialogFooter>
                                <Button type='submit' disabled={!newName.trim() || createCollection.isPending}>
                                    Create
                                </Button>
                            </DialogFooter>
                        </form>
                    </DialogContent>
                </Dialog>
            </div>

            <Link
                to={workspacePath(workspaceId, '/')}
                className='mb-4 flex items-center gap-2 rounded-lg border border-border bg-card px-4 py-3 text-sm font-medium text-foreground transition-colors hover:bg-secondary'
            >
                <FolderOpen className='h-4 w-4 text-primary' />
                <span className='flex-1'>All recipes</span>
                <ChevronRight className='h-4 w-4 text-muted-foreground' />
            </Link>

            {isLoading && <LoadingState label='Loading collections…' />}

            {!isLoading && collections.length === 0 && (
                <EmptyState
                    title='No collections yet'
                    description='Create a collection from the button above, then add recipes from the library or a recipe page.'
                />
            )}

            {!isLoading && collections.length > 0 && (
                <ul className='space-y-2'>
                    {collections.map(collection => (
                        <li key={collection.id}>
                            <Link
                                to={workspacePath(workspaceId, `collections/${collection.id}`)}
                                className='flex items-center gap-3 rounded-lg border border-border bg-card px-4 py-3 text-sm transition-colors hover:bg-secondary'
                            >
                                <span className='min-w-0 flex-1 truncate font-medium text-foreground'>
                                    {collection.name}
                                </span>
                                {!collection.isOwnedByViewerWorkspace ? (
                                    <span className='shrink-0 text-[10px] font-semibold uppercase text-muted-foreground'>
                                        Shared
                                    </span>
                                ) : null}
                                <span className='shrink-0 text-xs text-muted-foreground'>{collection.recipeCount}</span>
                                <ChevronRight className='h-4 w-4 shrink-0 text-muted-foreground' />
                            </Link>
                        </li>
                    ))}
                </ul>
            )}
        </div>
    );
}
