import { Minus, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

interface RecipeYieldScaleProps {
    /** Servings the recipe was written for (denominator for scaling). */
    baseServings: number;
    /** Currently selected yield in servings. */
    targetServings: number;
    onTargetServingsChange: (value: number) => void;
    className?: string;
}

function clampServings(value: number): number {
    if (!Number.isFinite(value)) return 1;
    return Math.min(99, Math.max(1, Math.round(value)));
}

function formatYieldRatio(targetServings: number, baseServings: number): string {
    if (!(baseServings > 0) || !Number.isFinite(targetServings)) return '1';
    const r = targetServings / baseServings;
    if (!Number.isFinite(r)) return '1';
    if (Math.abs(r - Math.round(r)) < 1e-6) return String(Math.round(r));
    return String(Number(r.toFixed(3)));
}

export function RecipeYieldScale({
    baseServings,
    targetServings,
    onTargetServingsChange,
    className,
}: RecipeYieldScaleProps) {
    const step = 1;

    return (
        <div className={cn('flex flex-col gap-1.5', className)}>
            <div className='flex flex-wrap items-center gap-2'>
                <span className='text-sm font-medium text-foreground'>Yield</span>
                <div className='flex items-center gap-1'>
                    <Button
                        type='button'
                        variant='outline'
                        size='icon'
                        className='h-8 w-8 shrink-0'
                        aria-label='Decrease servings'
                        onClick={() => onTargetServingsChange(clampServings(targetServings - step))}
                    >
                        <Minus className='h-4 w-4' />
                    </Button>
                    <Input
                        type='number'
                        min={1}
                        max={99}
                        step={1}
                        value={targetServings}
                        onChange={event => {
                            const n = Number(event.target.value);
                            onTargetServingsChange(clampServings(n));
                        }}
                        className='h-8 w-[4.5rem] text-center tabular-nums'
                    />
                    <Button
                        type='button'
                        variant='outline'
                        size='icon'
                        className='h-8 w-8 shrink-0'
                        aria-label='Increase servings'
                        onClick={() => onTargetServingsChange(clampServings(targetServings + step))}
                    >
                        <Plus className='h-4 w-4' />
                    </Button>
                </div>
                <span className='text-sm text-muted-foreground'>servings</span>
            </div>
            <p className='text-xs text-muted-foreground'>
                Recipe written for{' '}
                <span className='whitespace-nowrap tabular-nums font-medium text-foreground'>{baseServings}</span>{' '}
                {baseServings === 1 ? 'serving' : 'servings'}
                {Math.abs(targetServings - baseServings) > 0.001 ? (
                    <>
                        {' '}
                        · Ingredients scaled ×
                        <span className='whitespace-nowrap tabular-nums font-medium text-foreground'>
                            {formatYieldRatio(targetServings, baseServings)}
                        </span>
                    </>
                ) : null}
            </p>
        </div>
    );
}
