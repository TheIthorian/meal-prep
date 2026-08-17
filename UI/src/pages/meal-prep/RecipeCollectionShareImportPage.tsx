import { useEffect, useMemo, useRef, useState } from 'react';
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
import { ShareSignupPrompt } from '@/components/share/ShareSignupPrompt';

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

    // The prompt is counted once per share link. A background refetch hands back a fresh preview
    // object, so the token — not the object identity — is what decides whether this has fired.
    const promptCapturedForToken = useRef<string | null>(null);

    useEffect(() => {
        if (isAuthLoading || isSignedIn || !preview) return;
        if (promptCapturedForToken.current === shareToken) return;

        promptCapturedForToken.current = shareToken;
        capture(analyticsEvents.shareLinkAuthPrompted, { recipe_count: preview.recipeCount });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isAuthLoading, isSignedIn, preview, shareToken]);

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
        // pb-28 on small screens keeps the last recipe clear of the fixed prompt bar.
        <div className='mx-auto max-w-6xl space-y-4 px-4 pb-28 pt-10 md:px-8 lg:pb-10'>
            {!isAuthLoading && !isSignedIn && (
                <ShareSignupPrompt
                    returnUrl={sharePath}
                    headline={
                        preview.recipeCount === 1
                            ? 'Save this recipe to your library.'
                            : `Save all ${preview.recipeCount} recipes to your library.`
                    }
                    detail='Plan your week and turn them into a shopping list.'
                />
            )}

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

                {isSignedIn && (
                    <>
                        {/* The import controls stay at a form-like width even when the recipe grid is wide. */}
                        <div className='mt-5 max-w-md space-y-3'>
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

                        <div className='mt-6 max-w-md'>
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

            {preview.recipes.length > 0 && (
                <ul className='grid grid-cols-1 gap-2 sm:grid-cols-2 xl:grid-cols-3'>
                    {preview.recipes.map(recipe => {
                        const totalMinutes = (recipe.prepMinutes ?? 0) + (recipe.cookMinutes ?? 0);

                        return (
                            // min-w-0 on the grid item: a grid track's automatic minimum is its content, so
                            // without it a long title widens the column instead of truncating.
                            <li key={recipe.id} className='min-w-0'>
                                <Link
                                    to={`${sharePath}/recipes/${recipe.id}`}
                                    className='flex h-full items-center gap-3 rounded-xl border border-border bg-card p-3 transition-colors hover:bg-accent'
                                >
                                    {recipe.hasImage ? (
                                        <img
                                            src={recipeCollectionsApi.sharedRecipeImageUrl(shareToken, recipe.id, 400)}
                                            alt=''
                                            className='h-14 w-14 shrink-0 rounded-md object-cover'
                                        />
                                    ) : (
                                        <div className='flex h-14 w-14 shrink-0 items-center justify-center rounded-md bg-muted'>
                                            <ChefHat className='h-5 w-5 text-muted-foreground' aria-hidden />
                                        </div>
                                    )}
                                    <span className='min-w-0 flex-1'>
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
        </div>
    );
}
