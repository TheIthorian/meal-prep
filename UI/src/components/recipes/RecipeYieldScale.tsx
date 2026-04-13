import { Slider } from '@/components/ui/slider';
import { formatAmount } from '@/lib/meal-prep';
import { cn } from '@/lib/utils';

interface RecipeYieldScaleProps {
    /** Servings the recipe was written for (denominator for scaling). */
    baseServings: number;
    /** Currently selected yield in servings. */
    targetServings: number;
    onTargetServingsChange: (value: number) => void;
    className?: string;
}

/** Clamp to slider range ×⅛…×8 in servings; allow fractions so ×⅛ works for small base yields. */
function clampTargetServings(value: number, baseServings: number): number {
    if (!(baseServings > 0) || !Number.isFinite(value)) return Math.max(1, baseServings);
    const min = baseServings / 8;
    const max = Math.min(99, baseServings * 8);
    const clamped = Math.min(max, Math.max(min, value));
    return Number(clamped.toFixed(3));
}

function formatTargetServingsLabel(n: number): string {
    if (!Number.isFinite(n)) return '—';
    if (Math.abs(n - Math.round(n)) < 1e-4) return String(Math.round(n));
    return formatAmount(n);
}

function formatYieldRatio(targetServings: number, baseServings: number): string {
    if (!(baseServings > 0) || !Number.isFinite(targetServings)) return '1';
    const r = targetServings / baseServings;
    if (!Number.isFinite(r)) return '1';
    if (Math.abs(r - Math.round(r)) < 1e-6) return String(Math.round(r));
    return String(Number(r.toFixed(3)));
}

/** Maps multiplier ratio r to slider position 0–100; ⅛→0, 1→50, 8→100 on log₈ scale. */
function ratioToSliderPercent(ratio: number): number {
    if (!(ratio > 0) || !Number.isFinite(ratio)) return 50;
    const log8 = Math.log(ratio) / Math.LN2 / 3;
    const t = (log8 + 1) / 2;
    const pct = Math.round(t * 100);
    return Math.min(100, Math.max(0, pct));
}

function sliderPercentToMultiplier(percent: number): number {
    const t = percent / 100;
    return Math.pow(8, 2 * t - 1);
}

export function RecipeYieldScale({
    baseServings,
    targetServings,
    onTargetServingsChange,
    className,
}: RecipeYieldScaleProps) {
    const ratio = baseServings > 0 ? targetServings / baseServings : 1;
    const sliderPercent = ratioToSliderPercent(ratio);
    const scaleLabel = formatYieldRatio(targetServings, baseServings);
    const isScaled = Math.abs(targetServings - baseServings) > 0.001;

    return (
        <div
            className={cn(
                'rounded-2xl border border-border/60 bg-muted/25 p-4 shadow-sm backdrop-blur-sm dark:border-border/40 dark:bg-muted/15',
                className,
            )}
        >
            <div className='flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between sm:gap-6'>
                <div className='min-w-0 flex-1 space-y-3'>
                    <div>
                        <h3 className='text-sm font-semibold tracking-tight text-foreground'>Yield</h3>
                        <p className='mt-1 text-xs leading-relaxed text-muted-foreground'>
                            Recipe written for{' '}
                            <span className='whitespace-nowrap tabular-nums font-medium text-foreground'>
                                {baseServings}
                            </span>{' '}
                            {baseServings === 1 ? 'serving' : 'servings'}
                            {isScaled ? (
                                <>
                                    {' '}
                                    <span className='text-border'>·</span> ingredients ×
                                    <span className='whitespace-nowrap tabular-nums font-medium text-foreground'>
                                        {scaleLabel}
                                    </span>
                                </>
                            ) : null}
                        </p>
                    </div>

                    <div className='space-y-2.5'>
                        <div className='relative px-1 pt-0.5'>
                            {/* Tick marks aligned to log scale endpoints (⅛, 1, 8). */}
                            <div
                                className='pointer-events-none absolute inset-x-0 top-[calc(50%-2px)] flex justify-between px-[7px]'
                                aria-hidden
                            >
                                <span className='h-2 w-px rounded-full bg-muted-foreground/30' />
                                <span className='h-2 w-px rounded-full bg-muted-foreground/30' />
                                <span className='h-2 w-px rounded-full bg-muted-foreground/30' />
                            </div>
                            <Slider
                                min={0}
                                max={100}
                                step={1}
                                value={[sliderPercent]}
                                onValueChange={values => {
                                    const pct = values[0] ?? 50;
                                    const m = sliderPercentToMultiplier(pct);
                                    onTargetServingsChange(clampTargetServings(baseServings * m, baseServings));
                                }}
                                aria-label='Scale recipe yield'
                                trackClassName='h-2.5 border border-border/50 bg-background/80 shadow-inner dark:bg-background/40'
                                rangeClassName='bg-gradient-to-r from-primary/85 to-primary shadow-[0_0_12px_-2px_hsl(var(--primary)/0.45)]'
                                thumbClassName='h-[1.375rem] w-[1.375rem] border-primary/90 bg-background shadow-md ring-4 ring-background/80 transition-[box-shadow,transform] hover:scale-105 hover:shadow-lg active:scale-95'
                            />
                        </div>
                        <div className='flex justify-between px-0.5 font-mono text-[11px] font-medium tabular-nums tracking-tight text-muted-foreground'>
                            <span className='whitespace-nowrap'>×⅛</span>
                            <span className='whitespace-nowrap text-foreground/80'>×1</span>
                            <span className='whitespace-nowrap'>×8</span>
                        </div>
                    </div>
                </div>

                <div className='flex shrink-0 flex-col items-stretch rounded-xl border border-border/50 bg-background/60 px-4 py-3 text-center shadow-sm dark:bg-background/30 sm:min-w-[7.5rem] sm:items-center sm:py-3.5'>
                    <span className='text-[10px] font-medium uppercase tracking-widest text-muted-foreground'>
                        Servings
                    </span>
                    <span className='mt-0.5 text-3xl font-semibold leading-none tracking-tight tabular-nums text-foreground'>
                        {formatTargetServingsLabel(targetServings)}
                    </span>
                </div>
            </div>
        </div>
    );
}
