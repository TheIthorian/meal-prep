import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FolderPlus, Loader2 } from 'lucide-react';
import { recipeCollectionsApi } from '@/lib/api';
import { Button } from '@/components/ui/button';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';

interface AddToRecipeCollectionMenuProps {
    workspaceId: string;
    recipeId: string;
    variant?: 'icon' | 'compact';
    className?: string;
}

export function AddToRecipeCollectionMenu({
    workspaceId,
    recipeId,
    variant = 'icon',
    className = '',
}: AddToRecipeCollectionMenuProps) {
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();

    const { data: collections = [], isLoading } = useQuery({
        queryKey: ['recipe-collections', workspaceId],
        queryFn: () => recipeCollectionsApi.list(workspaceId),
        enabled: Boolean(workspaceId),
    });

    const ownedCollections = collections.filter(c => c.isOwnedByViewerWorkspace);

    const addRecipe = useMutation({
        mutationFn: (collectionId: string) => recipeCollectionsApi.addRecipe(workspaceId, collectionId, recipeId),
        onSuccess: (_data, collectionId) => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-collection', workspaceId, collectionId] });
            const name = collections.find(c => c.id === collectionId)?.name ?? 'collection';
            toast({ title: `Added to “${name}”` });
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeAddedToCollection,
                    withWorkspaceProperties(currentWorkspace, {
                        collection_id: collectionId,
                        recipe_id: recipeId,
                    }),
                );
            }
        },
    });

    if (ownedCollections.length === 0 && !isLoading) {
        return null;
    }

    const menu = (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button
                    type='button'
                    variant={variant === 'icon' ? 'ghost' : 'outline'}
                    size={variant === 'icon' ? 'icon' : 'sm'}
                    className={className}
                    disabled={isLoading || ownedCollections.length === 0}
                    aria-label='Add to collection'
                >
                    {addRecipe.isPending ? (
                        <Loader2 className='h-4 w-4 animate-spin' />
                    ) : (
                        <FolderPlus className='h-4 w-4' />
                    )}
                    {variant === 'compact' ? <span className='ml-2'>Collection</span> : null}
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align='end' className='w-56'>
                <DropdownMenuLabel>Add to collection</DropdownMenuLabel>
                <DropdownMenuSeparator />
                {isLoading && (
                    <DropdownMenuItem disabled className='text-muted-foreground'>
                        Loading…
                    </DropdownMenuItem>
                )}
                {!isLoading &&
                    ownedCollections.map(collection => (
                        <DropdownMenuItem
                            key={collection.id}
                            disabled={addRecipe.isPending}
                            onClick={() => void addRecipe.mutateAsync(collection.id)}
                        >
                            <span className='truncate'>{collection.name}</span>
                            <span className='ml-auto pl-2 text-xs text-muted-foreground'>{collection.recipeCount}</span>
                        </DropdownMenuItem>
                    ))}
            </DropdownMenuContent>
        </DropdownMenu>
    );

    if (className) return <div className={className}>{menu}</div>;
    return menu;
}
