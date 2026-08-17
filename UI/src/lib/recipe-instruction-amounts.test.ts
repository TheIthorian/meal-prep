import { describe, expect, it } from 'vitest';
import { buildInstructionSegments, findInstructionIngredientMatches } from '@/lib/recipe-instruction-amounts';
import { instructionMatchFixtures, toRecipeIngredients } from '@/lib/__fixtures__/instruction-ingredient-fixtures';

describe('findInstructionIngredientMatches', () => {
    for (const fixture of instructionMatchFixtures) {
        describe(fixture.recipe, () => {
            const ingredients = toRecipeIngredients(fixture.ingredients);

            for (const step of fixture.steps) {
                it(`matches the expected ingredients in "${step.text}"`, () => {
                    const matches = findInstructionIngredientMatches(step.text, ingredients);
                    expect(matches.map(match => match.ingredient.name)).toEqual(step.expected);
                });
            }
        });
    }

    it('returns matches ordered by position with spans covering the matched words', () => {
        const ingredients = toRecipeIngredients([{ name: 'spring onions' }, { name: 'garlic' }]);
        const instruction = 'Fry the garlic, then add the spring onions.';

        const matches = findInstructionIngredientMatches(instruction, ingredients);

        expect(matches).toHaveLength(2);
        expect(instruction.slice(matches[0].start, matches[0].end)).toBe('garlic');
        expect(instruction.slice(matches[1].start, matches[1].end)).toBe('spring onions');
    });

    it('ignores ingredients whose names are empty once normalized', () => {
        const ingredients = toRecipeIngredients([{ name: '   ' }, { name: 'salt' }]);

        const matches = findInstructionIngredientMatches('Add a pinch of salt.', ingredients);

        expect(matches.map(match => match.ingredient.name)).toEqual(['salt']);
    });

    it('does not match an ingredient inside a longer unrelated word', () => {
        const ingredients = toRecipeIngredients([{ name: 'oil' }]);

        expect(findInstructionIngredientMatches('Boil the kettle.', ingredients)).toEqual([]);
    });
});

describe('buildInstructionSegments', () => {
    it('appends a bracketed amount after each matched ingredient', () => {
        const ingredients = toRecipeIngredients([{ name: 'olive oil', amount: 2, unit: 'tbsp' }]);

        const segments = buildInstructionSegments('Heat the olive oil gently.', ingredients);

        expect(segments.filter(segment => segment.kind === 'bracket')).toEqual([
            expect.objectContaining({ kind: 'bracket', bracket: '[2 tbsp]' }),
        ]);
        expect(segments.map(segment => (segment.kind === 'text' ? segment.text : '')).join('')).toBe(
            'Heat the olive oil gently.',
        );
    });

    it('leaves prose untouched when an amount is missing', () => {
        const ingredients = toRecipeIngredients([{ name: 'salt' }]);

        const segments = buildInstructionSegments('Season with salt.', ingredients);

        expect(segments.every(segment => segment.kind === 'text')).toBe(true);
        expect(segments.map(segment => (segment.kind === 'text' ? segment.text : '')).join('')).toBe(
            'Season with salt.',
        );
    });

    it('matches wording that differs from the ingredient list', () => {
        const ingredients = toRecipeIngredients([{ name: 'chicken thighs', amount: 4 }]);

        const segments = buildInstructionSegments('Brown the chicken thigh pieces.', ingredients);

        expect(segments.some(segment => segment.kind === 'bracket')).toBe(true);
    });
});
