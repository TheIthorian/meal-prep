import { getIngredientListAmountText, getIngredientListLabel } from '@/lib/meal-prep';
import type { RecipeIngredient } from '@/models/meal-prep';

interface RecipeIngredientListRowProps {
    ingredient: RecipeIngredient;
}

export function RecipeIngredientListRow({ ingredient }: RecipeIngredientListRowProps) {
    const label = getIngredientListLabel(ingredient);
    const amountText = getIngredientListAmountText(ingredient);

    return (
        <>
            {label}
            {amountText ? (
                <>
                    {' '}
                    <span className='whitespace-nowrap tabular-nums text-muted-foreground'>{amountText}</span>
                </>
            ) : null}
        </>
    );
}
