import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ChefHat } from 'lucide-react';
import { recipeCollectionsApi } from '@/lib/api';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics } from '@/lib/analytics';
import { buildAuthPath } from '@/lib/return-url';

export default function RecipeCollectionShareImportPage() {
    const { shareToken = '' } = useParams<{ shareToken: string }>();
    const navigate = useNavigate();
    const { user, isLoading: isAuthLoading } = useAuth();
    const { workspaces, currentWorkspace } = useWorkspace();
    const [targetWorkspaceId, setTargetWorkspaceId] = useState(currentWorkspace?.workspaceId ?? '');
    const { capture } = useAnalytics();

    const sharePath = `/share/recipe-collections/${shareToken}`;
    const isSignedIn = Boolean(user);

    const { data: preview, isLoading } = useQuery({
        queryKey: ['recipe-collection-share-preview', shareToken],
        queryFn: () => recipeCollectionsApi.getShareLinkPreview(shareToken),
        enabled: Boolean(shareToken),
    });

    useEffect(() => {
        if (!targetWorkspaceId && currentWorkspace?.workspaceId) {
            setTargetWorkspaceId(currentWorkspace.workspaceId);
        }
    }, [currentWorkspace?.workspaceId, targetWorkspaceId]);

    useEffect(() => {
        if (isAuthLoading || isSignedIn || !preview) return;

        capture(analyticsEvents.shareLinkAuthPrompted, { recipe_count: preview.recipeCount });
        // Prompt is shown once per loaded share link; capture must not re-fire on unrelated renders.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isAuthLoading, isSignedIn, preview]);

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
        <div className='mx-auto max-w-xl space-y-4 px-4 py-10 md:px-8'>
            <div className='rounded-xl border border-border bg-card p-6'>
                <h1 className='font-heading text-2xl text-foreground'>
                    {isSignedIn ? 'Import shared collection' : 'Shared recipe collection'}
                </h1>
                <p className='mt-2 text-sm text-muted-foreground'>
                    <span className='font-medium text-foreground'>{preview.collectionName}</span> from{' '}
                    {preview.ownerWorkspaceName}
                </p>
                {preview.description && <p className='mt-1 text-sm text-muted-foreground'>{preview.description}</p>}
                <p className='mt-1 text-sm text-muted-foreground'>{preview.recipeCount} recipes</p>

                {preview.recipes.length > 0 && (
                    <ul className='mt-4 space-y-2'>
                        {preview.recipes.map(recipe => {
                            const totalMinutes = (recipe.prepMinutes ?? 0) + (recipe.cookMinutes ?? 0);

                            return (
                                <li key={recipe.id}>
                                    <Link
                                        to={`${sharePath}/recipes/${recipe.id}`}
                                        className='flex items-center gap-3 rounded-lg border border-border bg-background p-2 transition-colors hover:bg-accent'
                                    >
                                        {recipe.hasImage ? (
                                            <img
                                                src={recipeCollectionsApi.sharedRecipeImageUrl(
                                                    shareToken,
                                                    recipe.id,
                                                    400,
                                                )}
                                                alt=''
                                                className='h-14 w-14 shrink-0 rounded-md object-cover'
                                            />
                                        ) : (
                                            <div className='flex h-14 w-14 shrink-0 items-center justify-center rounded-md bg-muted'>
                                                <ChefHat className='h-5 w-5 text-muted-foreground' aria-hidden />
                                            </div>
                                        )}
                                        <span className='min-w-0'>
                                            <span className='block truncate text-sm font-medium text-foreground'>
                                                {recipe.title}
                                            </span>
                                            {recipe.description && (
                                                <span className='block truncate text-xs text-muted-foreground'>
                                                    {recipe.description}
                                                </span>
                                            )}
                                            {totalMinutes > 0 && (
                                                <span className='block text-xs text-muted-foreground'>
                                                    {totalMinutes} min
                                                </span>
                                            )}
                                        </span>
                                    </Link>
                                </li>
                            );
                        })}
                    </ul>
                )}

                {isSignedIn && (
                    <>
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
                    </>
                )}
            </div>

            {!isAuthLoading && !isSignedIn && (
                <div className='rounded-xl border border-border bg-card p-6 text-center'>
                    <div className='flex justify-center'>
                        <div className='rounded-full bg-primary p-3'>
                            <ChefHat className='h-6 w-6 text-primary-foreground' aria-hidden />
                        </div>
                    </div>
                    <h2 className='mt-4 font-heading text-xl text-foreground'>
                        Save these recipes to your own library
                    </h2>
                    <p className='mt-2 text-sm text-muted-foreground'>
                        Create a free Meal Prep account to import this collection, plan your week and turn it into a
                        shopping list. You will come straight back here.
                    </p>
                    <div className='mt-5 flex flex-col gap-3 sm:flex-row sm:justify-center'>
                        <Button asChild className='sm:min-w-40'>
                            <Link to={buildAuthPath('/register', sharePath)}>Create free account</Link>
                        </Button>
                        <Button asChild variant='outline' className='sm:min-w-40'>
                            <Link to={buildAuthPath('/login', sharePath)}>Sign in</Link>
                        </Button>
                    </div>
                </div>
            )}
        </div>
    );
}
