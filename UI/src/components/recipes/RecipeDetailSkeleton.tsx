import { Skeleton } from '@/components/ui/skeleton';

const INGREDIENT_ROW_WIDTHS = ['w-11/12', 'w-9/12', 'w-10/12', 'w-8/12', 'w-11/12', 'w-7/12'];
const STEP_WIDTHS = ['w-full', 'w-11/12', 'w-10/12', 'w-11/12'];

/**
 * Mirrors the recipe detail layout so the page does not jump when the data lands: header
 * actions, title block, photo, meta and nutrition on the left; actions, ingredients and
 * instructions on the right.
 */
export function RecipeDetailSkeleton() {
    return (
        <div
            className='mx-auto max-w-3xl px-4 py-6 md:px-8 md:py-10 xl:max-w-7xl'
            role='status'
            aria-live='polite'
            aria-busy='true'
        >
            <span className='sr-only'>Loading recipe…</span>

            <div className='mb-6 flex flex-wrap items-center justify-between gap-3' aria-hidden='true'>
                <Skeleton className='h-5 w-32' />
                <div className='flex items-center gap-2'>
                    <Skeleton className='h-9 w-9 rounded-md' />
                    <Skeleton className='h-9 w-9 rounded-md' />
                    <Skeleton className='h-9 w-9 rounded-md' />
                    <Skeleton className='h-9 w-9 rounded-md' />
                </div>
            </div>

            <div
                className='xl:grid xl:grid-cols-[minmax(0,26rem)_minmax(0,1fr)] xl:items-start xl:gap-12'
                aria-hidden='true'
            >
                <div className='min-w-0'>
                    <div className='mb-6'>
                        <div className='mb-3 flex flex-wrap gap-1.5'>
                            <Skeleton className='h-5 w-16 rounded-full' />
                            <Skeleton className='h-5 w-20 rounded-full' />
                            <Skeleton className='h-5 w-14 rounded-full' />
                        </div>
                        <Skeleton className='h-9 w-3/4 md:h-10' />
                    </div>

                    <Skeleton className='mb-6 aspect-[16/9] w-full rounded-xl' />

                    <div className='mb-8 space-y-3'>
                        <Skeleton className='h-4 w-full' />
                        <Skeleton className='h-4 w-5/6' />
                        <Skeleton className='h-4 w-40' />
                        <div className='flex flex-wrap items-center gap-6 pt-1'>
                            <Skeleton className='h-4 w-20' />
                            <Skeleton className='h-4 w-24' />
                        </div>
                        <Skeleton className='h-10 w-56 rounded-lg' />
                    </div>

                    <div className='mb-8'>
                        <Skeleton className='mb-4 h-6 w-48' />
                        <div className='grid grid-cols-2 gap-3 sm:grid-cols-4 xl:grid-cols-2'>
                            {['calories', 'protein', 'carbs', 'fat'].map(nutrient => (
                                <Skeleton key={nutrient} className='h-[4.5rem] rounded-lg' />
                            ))}
                        </div>
                    </div>
                </div>

                <div className='min-w-0'>
                    <div className='mb-8 flex items-center gap-3'>
                        <Skeleton className='h-12 flex-1 rounded-lg' />
                        <Skeleton className='h-10 w-40 rounded-md' />
                    </div>

                    <div className='grid gap-8 md:grid-cols-[280px_1fr]'>
                        <div>
                            <Skeleton className='mb-4 h-6 w-32' />
                            <div className='space-y-2.5 rounded-xl border border-border/50 bg-card p-4'>
                                {INGREDIENT_ROW_WIDTHS.map((width, i) => (
                                    <Skeleton key={i} className={`h-4 ${width}`} />
                                ))}
                            </div>
                        </div>

                        <div>
                            <Skeleton className='mb-4 h-6 w-32' />
                            <ol className='space-y-4'>
                                {STEP_WIDTHS.map((width, i) => (
                                    <li key={i} className='flex gap-3'>
                                        <Skeleton className='mt-0.5 h-7 w-7 flex-shrink-0 rounded-full' />
                                        <div className='min-w-0 flex-1 space-y-2'>
                                            <Skeleton className={`h-4 ${width}`} />
                                            <Skeleton className='h-4 w-2/3' />
                                        </div>
                                    </li>
                                ))}
                            </ol>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
