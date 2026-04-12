import { Link } from 'react-router-dom';
import { BookOpen, Users } from 'lucide-react';
import type { RecipeListItem } from '@/models/meal-prep';
import { motion } from 'framer-motion';

interface RecipeCardProps {
    workspaceId: string;
    recipe: RecipeListItem;
    index: number;
}

export function RecipeCard({ workspaceId, recipe, index }: RecipeCardProps) {
    const to = `/workspaces/${workspaceId}/recipe/${recipe.id}`;

    return (
        <motion.div
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.35, delay: index * 0.06, ease: 'easeOut' }}
        >
            <Link
                to={to}
                className='group block overflow-hidden rounded-xl border border-border/50 bg-card transition-all duration-200 hover:border-primary/20 hover:shadow-lg hover:shadow-primary/5'
            >
                <div className='aspect-[4/3] overflow-hidden bg-muted'>
                    <div className='flex h-full w-full items-center justify-center text-muted-foreground/30'>
                        <BookOpenPlaceholder />
                    </div>
                </div>
                <div className='p-4'>
                    <div className='mb-2 flex flex-wrap gap-1.5'>
                        {recipe.tags.slice(0, 2).map(tag => (
                            <span
                                key={tag}
                                className='rounded-full bg-primary/8 px-2 py-0.5 text-[11px] font-medium uppercase tracking-wider text-primary'
                            >
                                {tag}
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
