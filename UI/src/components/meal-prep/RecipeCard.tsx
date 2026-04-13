import { Link } from 'react-router-dom';
import { BookOpen, Star, Users } from 'lucide-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { RecipeListItem } from '@/models/meal-prep';
import { formatRecipeTagLabel } from '@/lib/meal-prep';
import { motion } from 'framer-motion';
import { RecipeCoverImage } from '@/components/meal-prep/RecipeCoverImage';
import { AddToRecipeCollectionMenu } from '@/components/meal-prep/AddToRecipeCollectionMenu';
import { recipesApi } from '@/lib/api';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';

interface RecipeCardProps {
    workspaceId: string;
    recipe: RecipeListItem;
    index: number;
}

export function RecipeCard({ workspaceId, recipe, index }: RecipeCardProps) {
    const to = `/workspaces/${workspaceId}/recipe/${recipe.id}`;
    const entranceDelay = Math.min(index, 5) * 0.02;
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();

    const setFavorite = useMutation({
        mutationFn: (next: boolean) => recipesApi.setFavorite(workspaceId, recipe.id, next),
        onSuccess: (_updated, next) => {
            void queryClient.invalidateQueries({ queryKey: ['recipes', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe', workspaceId, recipe.id] });
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeFavoriteUpdated,
                    withWorkspaceProperties(currentWorkspace, {
                        recipe_id: recipe.id,
                        is_favorite: next,
                    }),
                );
            }
        },
    });

    const starVisibility = recipe.isFavorite
        ? 'opacity-85 group-hover/card:opacity-100'
        : 'opacity-0 group-hover/card:opacity-100';

    return (
        <motion.div
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.2, delay: entranceDelay, ease: 'easeOut' }}
            className='group/card relative'
        >
            <Link
                to={to}
                className='group block overflow-hidden rounded-xl border border-border/50 bg-card transition-all duration-200 hover:border-primary/20 hover:shadow-lg hover:shadow-primary/5'
            >
                <div className='aspect-[4/3] overflow-hidden bg-muted'>
                    {recipe.hasImage ? (
                        <RecipeCoverImage
                            workspaceId={workspaceId}
                            recipeId={recipe.id}
                            hasImage
                            alt={`${recipe.title} cover`}
                            className='h-full w-full object-cover'
                        />
                    ) : (
                        <div className='flex h-full w-full items-center justify-center text-muted-foreground/30'>
                            <BookOpenPlaceholder />
                        </div>
                    )}
                </div>
                <div className='p-4'>
                    <div className='mb-2 flex flex-wrap gap-1.5'>
                        {recipe.tags.slice(0, 2).map(tag => (
                            <span
                                key={tag}
                                className='rounded-full bg-primary/8 px-2 py-0.5 text-[11px] font-medium uppercase tracking-wider text-primary'
                            >
                                {formatRecipeTagLabel(tag)}
                            </span>
                        ))}
                    </div>
                    <h3 className='font-heading line-clamp-1 text-lg text-card-foreground transition-colors group-hover:text-primary'>
                        {recipe.title}
                    </h3>
                    <p className='mt-1 line-clamp-2 text-sm text-muted-foreground'>{recipe.description ?? '—'}</p>
                    <div className='mt-3 flex items-center gap-4 text-xs text-muted-foreground'>
                        <span className='flex items-center gap-1'>
                            <Users className='h-3.5 w-3.5' />
                            {recipe.servings} servings
                        </span>
                    </div>
                </div>
            </Link>
            <AddToRecipeCollectionMenu
                workspaceId={workspaceId}
                recipeId={recipe.id}
                className='absolute left-3 top-3 z-10 opacity-0 transition-opacity group-hover/card:opacity-100 focus-within:opacity-100'
            />
            <button
                type='button'
                aria-label={recipe.isFavorite ? 'Remove from favourites' : 'Add to favourites'}
                aria-pressed={recipe.isFavorite}
                disabled={setFavorite.isPending}
                onClick={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    void setFavorite.mutateAsync(!recipe.isFavorite);
                }}
                className={`absolute right-3 top-3 z-10 rounded-md bg-background/90 p-1.5 text-amber-500 shadow-sm ring-1 ring-border/60 transition-opacity hover:bg-background focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 ${starVisibility}`}
            >
                <Star
                    className={`h-4 w-4 ${recipe.isFavorite ? 'fill-amber-400 text-amber-500' : 'text-amber-600/90'}`}
                />
            </button>
        </motion.div>
    );
}

function BookOpenPlaceholder() {
    return (
        <svg width='48' height='48' fill='none' viewBox='0 0 24 24' stroke='currentColor' strokeWidth='1.5'>
            <path d='M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253' />
        </svg>
    );
}
