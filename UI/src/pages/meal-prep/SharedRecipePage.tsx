import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import { BookmarkPlus } from 'lucide-react';
import { recipeSharesApi } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { toast } from '@/hooks/use-toast';
import { ShareSignupPrompt } from '@/components/share/ShareSignupPrompt';
import { SharedRecipeNotice } from '@/components/share/SharedRecipeNotice';
import { SharedRecipeView } from '@/components/share/SharedRecipeView';
import { MealPrepBottomNav } from '@/components/meal-prep/MealPrepBottomNav';
import { MealPrepTopNav } from '@/components/meal-prep/MealPrepTopNav';

export default function SharedRecipePage() {
    const { shareToken = '' } = useParams<{ shareToken: string }>();
    const navigate = useNavigate();
    const { user, isLoading: isAuthLoading } = useAuth();
    const { workspaces, currentWorkspace } = useWorkspace();
    const [targetWorkspaceId, setTargetWorkspaceId] = useState(currentWorkspace?.workspaceId ?? '');

    const sharePath = `/share/recipes/${shareToken}`;
    const isSignedIn = Boolean(user);

    const { data: preview, isLoading } = useQuery({
        queryKey: ['shared-recipe-link', shareToken],
        queryFn: () => recipeSharesApi.getSharedRecipe(shareToken),
        enabled: Boolean(shareToken),
    });

    useEffect(() => {
        if (!targetWorkspaceId && currentWorkspace?.workspaceId) {
            setTargetWorkspaceId(currentWorkspace.workspaceId);
        }
    }, [currentWorkspace?.workspaceId, targetWorkspaceId]);

    const saveMutation = useMutation({
        mutationFn: () => recipeSharesApi.saveToWorkspace(targetWorkspaceId, shareToken),
        onSuccess: recipe => {
            toast({ title: 'Saved to your recipes' });
            navigate(`/workspaces/${targetWorkspaceId}/recipe/${recipe.id}`);
        },
        onError: () => {
            toast({ title: 'Could not save the recipe', variant: 'destructive' });
        },
    });

    if (isLoading) {
        return (
            <div className='mx-auto max-w-2xl px-4 py-10 md:px-8'>
                <LoadingState label='Loading recipe…' />
            </div>
        );
    }

    if (!preview) {
        return (
            <div className='mx-auto max-w-2xl px-4 py-10 md:px-8'>
                <EmptyState title='Recipe not found' description='The link may be invalid or expired.' />
            </div>
        );
    }

    const { recipe } = preview;

    return (
        <>
            {/* A signed-in visitor following a share link is outside the workspace layout, so the
                header and tab bar are rendered here for them to get back into the app. */}
            {isSignedIn && currentWorkspace && <MealPrepTopNav workspaceId={currentWorkspace.workspaceId} />}

            {/* pb-28 on small screens keeps the last of the recipe clear of the fixed prompt bar
            (signed out) or the mobile tab bar (signed in). */}
            <div className='mx-auto max-w-5xl space-y-4 px-4 pb-28 pt-10 md:px-8 lg:pb-10'>
                {!isAuthLoading && !isSignedIn && (
                    <ShareSignupPrompt
                        returnUrl={sharePath}
                        headline='Save this recipe to your library.'
                        detail='Plan your week and turn it into a shopping list.'
                    />
                )}

                <SharedRecipeView
                    recipe={recipe}
                    imageUrl={recipe.hasImage ? recipeSharesApi.sharedRecipeImageUrl(shareToken, 800) : null}
                    header={
                        <SharedRecipeNotice
                            ownerWorkspaceName={preview.ownerWorkspaceName}
                            cookingPath={`${sharePath}/cooking`}
                            returnUrl={sharePath}
                            isSignedIn={isSignedIn}
                            saveAction={
                                isSignedIn ? (
                                    <div className='flex flex-wrap items-center gap-2'>
                                        {workspaces.length > 1 && (
                                            <Select value={targetWorkspaceId} onValueChange={setTargetWorkspaceId}>
                                                <SelectTrigger className='h-9 w-52'>
                                                    <SelectValue placeholder='Choose workspace…' />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {workspaces.map(workspace => (
                                                        <SelectItem
                                                            key={workspace.workspaceId}
                                                            value={workspace.workspaceId}
                                                        >
                                                            {workspace.name}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        )}
                                        <Button
                                            type='button'
                                            size='sm'
                                            disabled={!targetWorkspaceId || saveMutation.isPending}
                                            onClick={() => void saveMutation.mutateAsync()}
                                        >
                                            <BookmarkPlus className='mr-1.5 h-4 w-4' aria-hidden />
                                            Save to my recipes
                                        </Button>
                                    </div>
                                ) : null
                            }
                        />
                    }
                />
            </div>

            {isSignedIn && currentWorkspace && <MealPrepBottomNav workspaceId={currentWorkspace.workspaceId} />}
        </>
    );
}
