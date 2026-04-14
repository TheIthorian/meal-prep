import { useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import { recipeCollectionsApi } from '@/lib/api';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { Button } from '@/components/ui/button';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { toast } from '@/hooks/use-toast';
import { useState } from 'react';

export default function RecipeCollectionShareImportPage() {
    const { shareToken = '' } = useParams<{ shareToken: string }>();
    const navigate = useNavigate();
    const { workspaces, currentWorkspace } = useWorkspace();
    const [targetWorkspaceId, setTargetWorkspaceId] = useState(currentWorkspace?.workspaceId ?? '');

    const { data: preview, isLoading } = useQuery({
        queryKey: ['recipe-collection-share-preview', shareToken],
        queryFn: () => recipeCollectionsApi.getShareLinkPreview(shareToken),
        enabled: Boolean(shareToken),
    });

    const importMutation = useMutation({
        mutationFn: () => recipeCollectionsApi.importFromShareLink(targetWorkspaceId, shareToken),
        onSuccess: collection => {
            toast({ title: 'Collection imported' });
            navigate(`/workspaces/${targetWorkspaceId}/collections/${collection.id}`);
        },
        onError: () => {
            toast({ title: 'Import failed', variant: 'destructive' });
        },
    });

    const selectableWorkspaces = useMemo(() => workspaces, [workspaces]);

    if (isLoading) {
        return (
            <div className='mx-auto max-w-xl px-4 py-10 md:px-8'>
                <LoadingState label='Loading share link…' />
            </div>
        );
    }

    if (!preview) {
        return (
            <div className='mx-auto max-w-xl px-4 py-10 md:px-8'>
                <EmptyState title='Share link not found' description='The link may be invalid or expired.' />
            </div>
        );
    }

    return (
        <div className='mx-auto max-w-xl px-4 py-10 md:px-8'>
            <div className='rounded-xl border border-border bg-card p-6'>
                <h1 className='font-heading text-2xl text-foreground'>Import shared collection</h1>
                <p className='mt-2 text-sm text-muted-foreground'>
                    <span className='font-medium text-foreground'>{preview.collectionName}</span> from{' '}
                    {preview.ownerWorkspaceName}
                </p>
                <p className='mt-1 text-sm text-muted-foreground'>{preview.recipeCount} recipes</p>

                <div className='mt-5 space-y-3'>
                    <label className='text-sm font-medium text-foreground'>Import into workspace</label>
                    <Select value={targetWorkspaceId} onValueChange={setTargetWorkspaceId}>
                        <SelectTrigger>
                            <SelectValue placeholder='Choose workspace…' />
                        </SelectTrigger>
                        <SelectContent>
                            {selectableWorkspaces.map(workspace => (
                                <SelectItem key={workspace.workspaceId} value={workspace.workspaceId}>
                                    {workspace.name}
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>
                </div>

                <div className='mt-6'>
                    <Button
                        type='button'
                        className='w-full'
                        disabled={!targetWorkspaceId || importMutation.isPending}
                        onClick={() => void importMutation.mutateAsync()}
                    >
                        Import collection
                    </Button>
                </div>
            </div>
        </div>
    );
}
