import type { ReactNode } from 'react';
import { Clock, Users } from 'lucide-react';
import type { SharedRecipeDetail } from '@/models/meal-prep';
import { safeHttpUrlHref } from '@/lib/meal-prep';
import { RecipeIngredientListRow } from '@/components/recipes/RecipeIngredientListRow';
import { InstructionWithInlineAmounts } from '@/components/recipes/InstructionWithInlineAmounts';

interface SharedRecipeViewProps {
    recipe: SharedRecipeDetail;
    /** Where the cover image is served from, or null when the recipe has none. */
    imageUrl: string | null;
    /** Rendered above the title, inside the card: the read-only notice and whatever actions the page offers. */
    header?: ReactNode;
}

/**
 * A shared recipe as its recipient reads it. Read-only by construction — there is no edit affordance
 * anywhere in here — and shared by the two share links: a recipe inside a shared collection, and a
 * recipe shared on its own.
 */
export function SharedRecipeView({ recipe, imageUrl, header }: SharedRecipeViewProps) {
    const totalMinutes = (recipe.prepMinutes ?? 0) + (recipe.cookMinutes ?? 0);
    const sourceHref = safeHttpUrlHref(recipe.sourceUrl);

    return (
        <article className='overflow-hidden rounded-xl border border-border bg-card'>
            {imageUrl && <img src={imageUrl} alt='' className='h-56 w-full object-cover md:h-72' />}

            <div className='p-6'>
                {header}

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
                    <p className='mt-6 text-sm text-muted-foreground break-all'>
                        Source:{' '}
                        {sourceHref ? (
                            <a
                                href={sourceHref}
                                target='_blank'
                                rel='noreferrer noopener'
                                className='underline underline-offset-4'
                            >
                                {recipe.sourceUrl}
                            </a>
                        ) : (
                            recipe.sourceUrl
                        )}
                    </p>
                )}
            </div>
        </article>
    );
}
