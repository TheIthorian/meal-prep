import type { RecipeIngredient } from '@/models/meal-prep';

/**
 * Fixture shape for instruction/ingredient matching: an ingredient list plus the steps it is
 * matched against, with the ingredient names each step is expected to resolve to (in order of
 * appearance). Names in `expected` refer to `ingredients[].name`.
 */
export interface InstructionMatchFixture {
    recipe: string;
    ingredients: FixtureIngredient[];
    steps: FixtureStep[];
}

export interface FixtureIngredient {
    name: string;
    amount?: number | null;
    unit?: string | null;
    /** Server-supplied shopping-list normalization, when the imported row carried one. */
    normalizedIngredientName?: string | null;
}

export interface FixtureStep {
    text: string;
    expected: string[];
}

/**
 * Builds the `RecipeIngredient` shape the matcher consumes from the terser fixture shape.
 */
export function toRecipeIngredients(ingredients: FixtureIngredient[]): RecipeIngredient[] {
    return ingredients.map((ingredient, index) => ({
        id: `fixture-${index}`,
        sortOrder: index,
        name: ingredient.name,
        normalizedIngredientName: ingredient.normalizedIngredientName ?? null,
        amount: ingredient.amount ?? null,
        unit: ingredient.unit ?? null,
        preparationNote: null,
        section: null,
        displayText: [ingredient.amount, ingredient.unit, ingredient.name].filter(Boolean).join(' '),
    }));
}

export const instructionMatchFixtures: InstructionMatchFixture[] = [
    {
        recipe: 'Weeknight bolognese (imported)',
        ingredients: [
            { name: 'olive oil', amount: 1, unit: 'tbsp' },
            { name: 'onion', amount: 1 },
            { name: 'garlic', amount: 3, unit: 'cloves' },
            { name: 'beef mince', amount: 500, unit: 'g' },
            { name: 'chopped tomatoes', amount: 400, unit: 'g' },
            { name: 'tomato purée', amount: 2, unit: 'tbsp' },
            { name: 'spaghetti', amount: 300, unit: 'g' },
            { name: 'parmesan', amount: 30, unit: 'g' },
        ],
        steps: [
            {
                // Leading quantity + unit, and a preparation word in front of a plural.
                text: 'Heat 1 tbsp olive oil in a large pan and cook the chopped onions until soft.',
                expected: ['olive oil', 'onion'],
            },
            {
                // "garlic cloves" in the step vs "garlic" in the list.
                text: 'Add the garlic cloves and fry for another minute.',
                expected: ['garlic'],
            },
            {
                text: 'Stir in the beef mince and brown it all over.',
                expected: ['beef mince'],
            },
            {
                // "tomato purée" must win over the shorter "chopped tomatoes" core word.
                text: 'Tip in the tinned chopped tomatoes with the tomato puree and simmer.',
                expected: ['chopped tomatoes', 'tomato purée'],
            },
            {
                text: 'Meanwhile cook the spaghetti, then serve topped with finely grated parmesan.',
                expected: ['spaghetti', 'parmesan'],
            },
        ],
    },
    {
        recipe: 'Chicken stir fry (imported)',
        ingredients: [
            { name: 'spring onions', amount: 4 },
            { name: 'onion', amount: 1 },
            { name: 'garlic', amount: 2, unit: 'cloves' },
            { name: 'ginger', amount: 1, unit: 'thumb' },
            { name: 'soy sauce', amount: 2, unit: 'tbsp' },
            { name: 'chicken thighs', amount: 6 },
            { name: 'sesame oil', amount: 1, unit: 'tsp' },
        ],
        steps: [
            {
                // "spring onion" must not be claimed by "onion".
                text: 'Slice the spring onions on the diagonal and set aside.',
                expected: ['spring onions'],
            },
            {
                text: 'Fry the diced onion until it starts to colour.',
                expected: ['onion'],
            },
            {
                // Singular in the step, plural in the list.
                text: 'Add the chicken thigh pieces and stir-fry for five minutes.',
                expected: ['chicken thighs'],
            },
            {
                text: 'Splash in 2 tbsp soy sauce and a drizzle of sesame oil.',
                expected: ['soy sauce', 'sesame oil'],
            },
            {
                text: 'Grate in the fresh ginger and the garlic.',
                expected: ['ginger', 'garlic'],
            },
        ],
    },
    {
        recipe: 'Victoria sponge (imported, ambiguous list)',
        ingredients: [
            // Two rows with the same name — a real import artefact. Neither should be matched.
            { name: 'butter', amount: 200, unit: 'g', normalizedIngredientName: 'butter' },
            { name: 'butter', amount: 50, unit: 'g', normalizedIngredientName: 'butter' },
            { name: 'plain flour', amount: 200, unit: 'g', normalizedIngredientName: 'plain flour' },
            { name: 'caster sugar', amount: 200, unit: 'g', normalizedIngredientName: 'caster sugar' },
            { name: 'eggs', amount: 4, normalizedIngredientName: 'eggs' },
            { name: 'strawberry jam', amount: 4, unit: 'tbsp', normalizedIngredientName: 'strawberry jam' },
        ],
        steps: [
            {
                // Ambiguous between the two butter rows, so it stays unmatched.
                text: 'Cream the softened butter with the caster sugar until pale.',
                expected: ['caster sugar'],
            },
            {
                text: 'Beat in the eggs one at a time, then fold in the sifted plain flour.',
                expected: ['eggs', 'plain flour'],
            },
            {
                // "sugar" and "jam" alone are partial matches and must not resolve.
                text: 'Dust with sugar and spread with jam once completely cool.',
                expected: [],
            },
            {
                text: 'Sandwich the sponges together with the strawberry jam.',
                expected: ['strawberry jam'],
            },
        ],
    },
    {
        recipe: 'Roast vegetable traybake (imported)',
        ingredients: [
            { name: 'red peppers', amount: 2 },
            { name: 'black pepper', unit: 'pinch' },
            { name: 'salt', unit: 'pinch' },
            { name: 'new potatoes', amount: 750, unit: 'g' },
            { name: 'rosemary', amount: 2, unit: 'sprigs' },
            { name: 'olive oil', amount: 3, unit: 'tbsp' },
        ],
        steps: [
            {
                // Bare "pepper" is ambiguous between red peppers and black pepper.
                text: 'Season generously with salt and pepper.',
                expected: ['salt'],
            },
            {
                text: 'Halve the red peppers and add them to the tray with the new potatoes.',
                expected: ['red peppers', 'new potatoes'],
            },
            {
                text: 'Drizzle over 3 tbsp of olive oil and scatter with roughly chopped rosemary.',
                expected: ['olive oil', 'rosemary'],
            },
            {
                // Nothing from the list appears here.
                text: 'Roast for 35 minutes until the edges are catching.',
                expected: [],
            },
        ],
    },
];
