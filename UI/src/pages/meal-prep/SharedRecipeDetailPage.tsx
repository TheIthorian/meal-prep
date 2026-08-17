import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Clock, Users } from 'lucide-react';
import { recipeCollectionsApi } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { RecipeIngredientListRow } from '@/components/recipes/RecipeIngredientListRow';
import { InstructionWithInlineAmounts } from '@/components/recipes/InstructionWithInlineAmounts';
import { ShareSignupPrompt } from '@/components/share/ShareSignupPrompt';

export default function SharedRecipeDetailPage() {
    const { shareToken = '', recipeId = '' } = useParams<{ shareToken: string; recipeId: string }>();
    const { user, isLoading: isAuthLoading } = useAuth();

    const collectionPath = `/share/recipe-collections/${shareToken}`;
    const recipePath = `${collectionPath}/recipes/${recipeId}`;
    const isSignedIn = Boolean(user);

    const { data: recipe, isLoading } = useQuery({
        queryKey: ['shared-recipe', shareToken, recipeId],
        queryFn: () => recipeCollectionsApi.getSharedRecipe(shareToken, recipeId),
        enabled: Boolean(shareToken && recipeId),
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

    const totalMinutes = (recipe.prepMinutes ?? 0) + (recipe.cookMinutes ?? 0);

    return (
        // pb-28 on small screens keeps the last of the recipe clear of the fixed prompt bar.
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

            <article className='overflow-hidden rounded-xl border border-border bg-card'>
                {recipe.hasImage && (
                    <img
                        src={recipeCollectionsApi.sharedRecipeImageUrl(shareToken, recipe.id, 800)}
                        alt=''
                        className='h-56 w-full object-cover md:h-72'
                    />
                )}

                <div className='p-6'>
                    <h1 className='font-heading text-2xl text-foreground'>{recipe.title}</h1>
                    {recipe.description && <p className='mt-2 text-sm text-muted-foreground'>{recipe.description}</p>}

                    <div className='mt-4 flex flex-wrap gap-4 text-sm text-muted-foreground'>
                        <span className='flex items-center gap-1.5'>
                            <Users className='h-4 w-4' aria-hidden />
                            Serves {recipe.servings}
                        </span>
                        {totalMinutes > 0 && (
                            <span className='flex items-center gap-1.5'>
                                <Clock className='h-4 w-4' aria-hidden />
                                {totalMinutes} min
                            </span>
                        )}
                    </div>

                    {recipe.tags.length > 0 && (
                        <ul className='mt-4 flex flex-wrap gap-2'>
                            {recipe.tags.map(tag => (
                                <li
                                    key={tag}
                                    className='rounded-full border border-border px-2.5 py-0.5 text-xs text-muted-foreground'
                                >
                                    {tag}
                                </li>
                            ))}
                        </ul>
                    )}

                    {/* Ingredients sit beside the method once there is room; the method column keeps a
                        readable measure rather than stretching to the full page width. */}
                    <div className='mt-6 gap-8 lg:grid lg:grid-cols-[minmax(0,18rem)_minmax(0,1fr)] lg:items-start'>
                        {/* The ingredients rail sticks below the prompt bar as the method scrolls past. */}
                        {recipe.ingredients.length > 0 && (
                            <section className='lg:sticky lg:top-24'>
                                <h2 className='font-heading text-lg text-foreground'>Ingredients</h2>
                                <ul className='mt-3 space-y-2'>
                                    {recipe.ingredients.map(ingredient => (
                                        <li key={ingredient.id} className='text-sm text-foreground'>
                                            <RecipeIngredientListRow ingredient={ingredient} />
                                        </li>
                                    ))}
                                </ul>
                            </section>
                        )}

                        {recipe.steps.length > 0 && (
                            <section className='mt-6 lg:mt-0'>
                                <h2 className='font-heading text-lg text-foreground'>Method</h2>
                                <ol className='mt-3 space-y-4'>
                                    {recipe.steps.map((step, index) => (
                                        <li key={step.id} className='flex gap-3 text-sm text-foreground'>
                                            <span className='mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-medium text-muted-foreground'>
                                                {index + 1}
                                            </span>
                                            <p>
                                                <InstructionWithInlineAmounts
                                                    instruction={step.instruction}
                                                    scaledIngredients={recipe.ingredients}
                                                />
                                            </p>
                                        </li>
                                    ))}
                                </ol>
                            </section>
                        )}
                    </div>

                    {recipe.notes && (
                        <section className='mt-6'>
                            <h2 className='font-heading text-lg text-foreground'>Notes</h2>
                            <p className='mt-2 whitespace-pre-line text-sm text-muted-foreground'>{recipe.notes}</p>
                        </section>
                    )}

                    {recipe.sourceUrl && (
                        <p className='mt-6 text-sm text-muted-foreground'>
                            Source:{' '}
                            <a
                                href={recipe.sourceUrl}
                                target='_blank'
                                rel='noreferrer noopener'
                                className='underline underline-offset-4'
                            >
                                {recipe.sourceUrl}
                            </a>
                        </p>
                    )}
                </div>
            </article>
        </div>
    );
}
