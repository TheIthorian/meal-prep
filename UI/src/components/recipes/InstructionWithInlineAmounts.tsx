import { useMemo } from 'react';
import { buildInstructionSegments } from '@/lib/recipe-instruction-amounts';
import type { RecipeIngredient } from '@/models/meal-prep';

interface InstructionWithInlineAmountsProps {
    instruction: string;
    scaledIngredients: RecipeIngredient[];
}

export function InstructionWithInlineAmounts({ instruction, scaledIngredients }: InstructionWithInlineAmountsProps) {
    const segments = useMemo(
        () => buildInstructionSegments(instruction, scaledIngredients),
        [instruction, scaledIngredients],
    );

    return (
        <>
            {segments.map((seg, i) =>
                seg.kind === 'text' ? (
                    <span key={`t-${i}`}>{seg.text}</span>
                ) : (
                    <span key={seg.key} className='whitespace-nowrap font-medium text-muted-foreground'>
                        {' '}
                        {seg.bracket}
                    </span>
                ),
            )}
        </>
    );
}
