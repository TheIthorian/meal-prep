import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import { recipeCollectionsApi } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { ShareSignupPrompt } from '@/components/share/ShareSignupPrompt';
import { SharedRecipeNotice } from '@/components/share/SharedRecipeNotice';
import { SharedRecipeView } from '@/components/share/SharedRecipeView';
import { MealPrepBottomNav } from '@/components/meal-prep/MealPrepBottomNav';
import { MealPrepTopNav } from '@/components/meal-prep/MealPrepTopNav';
import { useWorkspace } from '@/contexts/WorkspaceContext';

export default function SharedRecipeDetailPage() {
    const { shareToken = '', recipeId = '' } = useParams<{ shareToken: string; recipeId: string }>();
    const { user, isLoading: isAuthLoading } = useAuth();
    const { currentWorkspace } = useWorkspace();

    const collectionPath = `/share/recipe-collections/${shareToken}`;
    const recipePath = `${collectionPath}/recipes/${recipeId}`;
    const isSignedIn = Boolean(user);

    const { data: recipe, isLoading } = useQuery({
        queryKey: ['shared-recipe', shareToken, recipeId],
        queryFn: () => recipeCollectionsApi.getSharedRecipe(shareToken, recipeId),
        enabled: Boolean(shareToken && recipeId),
    });

    // The collection preview names the workspace that shared it; it is already in the cache from the
    // collection page a visitor arrives through, and is refetched cheaply when they deep-link here.
    const { data: collectionPreview } = useQuery({
        queryKey: ['recipe-collection-share-preview', shareToken],
        queryFn: () => recipeCollectionsApi.getShareLinkPreview(shareToken),
        enabled: Boolean(shareToken),
    });

    if (isLoading) {
        return (
            <div className='mx-auto max-w-2xl px-4 py-10 md:px-8'>
                <LoadingState label='Loading recipe…' />
            </div>
        );
    }

    if (!recipe) {
        return (
            <div className='mx-auto max-w-2xl px-4 py-10 md:px-8'>
                <EmptyState title='Recipe not found' description='The link may be invalid or expired.' />
            </div>
        );
    }

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
                        returnUrl={recipePath}
                        headline='Save this recipe to your library.'
                        detail='Plan your week and turn it into a shopping list.'
                    />
                )}

                <Button asChild variant='ghost' size='sm' className='-ml-2'>
                    <Link to={collectionPath}>
                        <ArrowLeft className='mr-1 h-4 w-4' aria-hidden />
                        Back to collection
                    </Link>
                </Button>

                <SharedRecipeView
                    recipe={recipe}
                    imageUrl={
                        recipe.hasImage
                            ? recipeCollectionsApi.sharedRecipeImageUrl(shareToken, recipe.id, 800)
                            : null
                    }
                    header={
                        collectionPreview ? (
                            // Saving a single recipe out of a shared collection is the collection page's job:
                            // it imports the whole collection, so this page offers reading and cooking only.
                            <SharedRecipeNotice
                                ownerWorkspaceName={collectionPreview.ownerWorkspaceName}
                                cookingPath={`${recipePath}/cooking`}
                                returnUrl={recipePath}
                                isSignedIn={isSignedIn}
                            />
                        ) : null
                    }
                />
            </div>

            {isSignedIn && currentWorkspace && <MealPrepBottomNav workspaceId={currentWorkspace.workspaceId} />}
        </>
    );
}
