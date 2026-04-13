import { useState } from 'react';
import { NavLink, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FolderOpen, Plus } from 'lucide-react';
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
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';

function workspacePath(workspaceId: string, subPath: string) {
    const trimmed = subPath.replace(/^\//, '');
    return `/workspaces/${workspaceId}/${trimmed}`;
}

function navClassName(isActive: boolean) {
    return `flex min-w-0 items-center gap-2 rounded-lg px-3 py-2 text-sm transition-colors ${
        isActive ? 'bg-primary/10 font-medium text-primary' : 'text-muted-foreground hover:bg-secondary hover:text-foreground'
    }`;
}

export function MealPrepCollectionsSidebar() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();
    const [createOpen, setCreateOpen] = useState(false);
    const [newName, setNewName] = useState('');

    const { data: collections = [] } = useQuery({
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

    if (!workspaceId) return null;

    return (
        <aside className='hidden w-56 shrink-0 flex-col border-r border-border bg-card/40 md:flex'>
            <div className='flex flex-1 flex-col gap-1 p-3'>
                <p className='px-2 pb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground'>
                    Collections
                </p>
                <NavLink to={workspacePath(workspaceId, '/')} end className={({ isActive }) => navClassName(isActive)}>
                    <FolderOpen className='h-4 w-4 shrink-0' />
                    <span className='truncate'>All recipes</span>
                </NavLink>
                <div className='my-1 border-t border-border' />
                <div className='min-h-0 flex-1 space-y-0.5 overflow-y-auto'>
                    {collections.map(collection => (
                        <NavLink
                            key={collection.id}
                            to={workspacePath(workspaceId, `collections/${collection.id}`)}
                            className={({ isActive }) => navClassName(isActive)}
                        >
                            <span className='min-w-0 flex-1 truncate'>{collection.name}</span>
                            {!collection.isOwnedByViewerWorkspace ? (
                                <span className='shrink-0 text-[10px] font-medium uppercase text-muted-foreground'>
                                    Shared
                                </span>
                            ) : null}
                            <span className='shrink-0 text-xs text-muted-foreground'>{collection.recipeCount}</span>
                        </NavLink>
                    ))}
                </div>
                <Dialog open={createOpen} onOpenChange={setCreateOpen}>
                    <DialogTrigger asChild>
                        <Button type='button' variant='outline' size='sm' className='mt-2 w-full gap-1'>
                            <Plus className='h-4 w-4' />
                            New collection
                        </Button>
                    </DialogTrigger>
                    <DialogContent>
                        <form onSubmit={handleCreateSubmit}>
                            <DialogHeader>
                                <DialogTitle>New collection</DialogTitle>
                            </DialogHeader>
                            <div className='py-4'>
                                <Label htmlFor='collection-name'>Name</Label>
                                <Input
                                    id='collection-name'
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
        </aside>
    );
}
