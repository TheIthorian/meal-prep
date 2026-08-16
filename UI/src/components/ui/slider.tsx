import * as React from 'react';
import * as SliderPrimitive from '@radix-ui/react-slider';

import { cn } from '@/lib/utils';

type SliderProps = React.ComponentPropsWithoutRef<typeof SliderPrimitive.Root> & {
    trackClassName?: string;
    rangeClassName?: string;
    thumbClassName?: string;
};

/**
 * Radix puts `role="slider"` on the thumb, not the root, so labelling props have to travel with
 * it. Left on the root they described a `<span>` with no role at all: the accessible name never
 * reached the control, and `aria-valuetext` was not a permitted attribute where it landed.
 */
const Slider = React.forwardRef<React.ElementRef<typeof SliderPrimitive.Root>, SliderProps>(
    (
        {
            className,
            trackClassName,
            rangeClassName,
            thumbClassName,
            'aria-label': ariaLabel,
            'aria-labelledby': ariaLabelledBy,
            'aria-valuetext': ariaValueText,
            ...props
        },
        ref,
    ) => (
        <SliderPrimitive.Root
            ref={ref}
            className={cn('relative flex w-full touch-none select-none items-center', className)}
            {...props}
        >
            <SliderPrimitive.Track
                className={cn('relative h-2 w-full grow overflow-hidden rounded-full bg-secondary', trackClassName)}
            >
                <SliderPrimitive.Range className={cn('absolute h-full bg-primary', rangeClassName)} />
            </SliderPrimitive.Track>
            <SliderPrimitive.Thumb
                aria-label={ariaLabel}
                aria-labelledby={ariaLabelledBy}
                aria-valuetext={ariaValueText}
                className={cn(
                    'block h-5 w-5 rounded-full border-2 border-primary bg-background ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50',
                    thumbClassName,
                )}
            />
        </SliderPrimitive.Root>
    ),
);
Slider.displayName = SliderPrimitive.Root.displayName;

export { Slider };
