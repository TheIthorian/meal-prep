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

/** Discrete ingredient multipliers (left → right). */
const YIELD_MULTIPLIERS = [
    1 / 8,
    1 / 4,
    1 / 3,
    1 / 2,
    2 / 3,
    3 / 4,
    1,
    2,
    3,
    4,
    5,
    6,
    7,
    8,
] as const;

/** Shown under each tick (Unicode fractions where available). */
const YIELD_MULTIPLIER_LABELS = [
    '⅛',
    '¼',
    '⅓',
    '½',
    '⅔',
    '¾',
    '1',
    '2',
    '3',
    '4',
    '5',
    '6',
    '7',
    '8',
] as const;

const SLIDER_MAX = YIELD_MULTIPLIERS.length - 1;

/** Clamp servings to discrete multiplier range ×⅛…×8, capped at 99. */
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

function ratioToMultiplierIndex(ratio: number): number {
    if (!(ratio > 0) || !Number.isFinite(ratio)) return YIELD_MULTIPLIERS.indexOf(1);
    let best = 0;
    let bestDiff = Infinity;
    for (let i = 0; i < YIELD_MULTIPLIERS.length; i++) {
        const m = YIELD_MULTIPLIERS[i]!;
        const d = Math.abs(m - ratio);
        if (d < bestDiff) {
            bestDiff = d;
            best = i;
        }
    }
    return best;
}

function formatScaleLabelForIndex(index: number): string {
    const label = YIELD_MULTIPLIER_LABELS[index] ?? '1';
    return `×${label}`;
}

export function RecipeYieldScale({
    baseServings,
    targetServings,
    onTargetServingsChange,
    className,
}: RecipeYieldScaleProps) {
    const ratio = baseServings > 0 ? targetServings / baseServings : 1;
    const sliderIndex = ratioToMultiplierIndex(ratio);
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
                                    <span className='text-border'>·</span> ingredients{' '}
                                    <span className='whitespace-nowrap font-medium text-foreground'>
                                        {formatScaleLabelForIndex(sliderIndex)}
                                    </span>
                                </>
                            ) : null}
                        </p>
                    </div>

                    <div className='space-y-2'>
                        <div className='relative min-w-0 px-1 pt-0.5'>
                            <div
                                className='pointer-events-none absolute inset-x-1 top-[calc(50%-2px)] z-0 grid'
                                style={{ gridTemplateColumns: `repeat(${YIELD_MULTIPLIERS.length}, minmax(0, 1fr))` }}
                                aria-hidden
                            >
                                {YIELD_MULTIPLIER_LABELS.map(label => (
                                    <div key={label} className='flex justify-center'>
                                        <span className='h-2 w-px rounded-full bg-muted-foreground/30' />
                                    </div>
                                ))}
                            </div>
                            <Slider
                                min={0}
                                max={SLIDER_MAX}
                                step={1}
                                value={[sliderIndex]}
                                onValueChange={values => {
                                    const i = values[0] ?? YIELD_MULTIPLIERS.indexOf(1);
                                    const m = YIELD_MULTIPLIERS[Math.min(SLIDER_MAX, Math.max(0, i))]!;
                                    onTargetServingsChange(clampTargetServings(baseServings * m, baseServings));
                                }}
                                aria-valuetext={`Scale ingredients ${formatScaleLabelForIndex(sliderIndex)}`}
                                aria-label='Scale recipe yield'
                                trackClassName='h-2.5 border border-border/50 bg-background/80 shadow-inner dark:bg-background/40'
                                rangeClassName='bg-gradient-to-r from-primary/85 to-primary shadow-[0_0_12px_-2px_hsl(var(--primary)/0.45)]'
                                thumbClassName='z-10 h-[1.375rem] w-[1.375rem] border-primary/90 bg-background shadow-md ring-4 ring-background/80 transition-[box-shadow,transform] hover:scale-105 hover:shadow-lg active:scale-95'
                            />
                        </div>
                        <div className='-mx-1 overflow-x-auto pb-0.5'>
                            <div
                                className='grid min-w-[min(100%,520px)] font-mono text-[9px] font-medium tabular-nums tracking-tight text-muted-foreground sm:min-w-0 sm:text-[10px]'
                                style={{ gridTemplateColumns: `repeat(${YIELD_MULTIPLIERS.length}, minmax(0, 1fr))` }}
                            >
                                {YIELD_MULTIPLIER_LABELS.map((label, i) => (
                                    <span
                                        key={label}
                                        className={cn(
                                            'block text-center leading-none',
                                            i === sliderIndex && 'font-semibold text-foreground',
                                        )}
                                    >
                                        ×{label}
                                    </span>
                                ))}
                            </div>
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
