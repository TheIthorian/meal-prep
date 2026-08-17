import type { RecipeIngredient } from '@/models/meal-prep';

/**
 * Words that describe how much of something is used. They carry no identity of their own, so they
 * are dropped from both ingredient names and instruction prose before matching.
 */
const UNIT_WORDS = new Set([
    'g',
    'gram',
    'grams',
    'kg',
    'kilogram',
    'kilograms',
    'oz',
    'ounce',
    'ounces',
    'lb',
    'lbs',
    'pound',
    'pounds',
    'ml',
    'milliliter',
    'milliliters',
    'millilitre',
    'millilitres',
    'l',
    'liter',
    'liters',
    'litre',
    'litres',
    'floz',
    'tsp',
    'teaspoon',
    'teaspoons',
    'tbsp',
    'tablespoon',
    'tablespoons',
    'cup',
    'cups',
    'item',
    'items',
    'unit',
    'units',
    'clove',
    'cloves',
    'can',
    'cans',
    'tin',
    'tins',
    'jar',
    'jars',
    'packet',
    'packets',
    'pack',
    'packs',
    'pinch',
    'pinches',
    'handful',
    'handfuls',
    'bunch',
    'bunches',
    'sprig',
    'sprigs',
    'stick',
    'sticks',
    'slice',
    'slices',
    'piece',
    'pieces',
    'chunk',
    'chunks',
    'wedge',
    'wedges',
    'strip',
    'strips',
    'knob',
    'knobs',
    'dash',
    'dashes',
    'splash',
    'splashes',
    'drizzle',
    'thumb',
    'bulb',
    'bulbs',
    'head',
    'heads',
]);

/**
 * Preparation and grading words that recipes sprinkle over ingredient names inconsistently
 * ("finely chopped onion" vs "onion"). Skipped on both sides of a match.
 */
const QUALIFIER_WORDS = new Set([
    'about',
    'approx',
    'approximately',
    'beaten',
    'boneless',
    'chilled',
    'chopped',
    'cold',
    'cooked',
    'crushed',
    'cubed',
    'deseeded',
    'diced',
    'drained',
    'dried',
    'extra',
    'fine',
    'finely',
    'firm',
    'free',
    'fresh',
    'freshly',
    'frozen',
    'good',
    'grated',
    'ground',
    'halved',
    'heaped',
    'hot',
    'juiced',
    'large',
    'lean',
    'level',
    'lukewarm',
    'medium',
    'melted',
    'minced',
    'optional',
    'organic',
    'peeled',
    'plus',
    'quality',
    'quartered',
    'range',
    'raw',
    'rinsed',
    'ripe',
    'roughly',
    'salted',
    'seeded',
    'shredded',
    'sifted',
    'skinless',
    'sliced',
    'small',
    'softened',
    'thickly',
    'thinly',
    'tinned',
    'toasted',
    'torn',
    'trimmed',
    'unsalted',
    'warm',
    'washed',
    'zested',
]);

/** Function words that may sit inside a name without changing what it refers to. */
const FILLER_WORDS = new Set(['a', 'an', 'the', 'of', 'or', 'into', 'in', 'to', 'for', 'with']);

/** Plurals that the suffix rules below would mangle. */
const IRREGULAR_SINGULARS: Record<string, string> = {
    chilies: 'chili',
    chillies: 'chilli',
    halves: 'half',
    knives: 'knife',
    leaves: 'leaf',
    loaves: 'loaf',
    potatoes: 'potato',
    tomatoes: 'tomato',
};

/**
 * Mirrors the shopping-list ingredient normalization (`MeasurementService.NormalizeIngredientName`):
 * lower-case, replace anything that is not a letter or digit with a space, and collapse runs of
 * whitespace. Accents are folded first so an imported "purée" matches a step that writes "puree".
 */
export function normalizeIngredientName(name: string): string {
    return name
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .toLowerCase()
        .replace(/[^a-z0-9\s]/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();
}

/**
 * The normalized name used for matching, preferring the value the API already computed for the
 * shopping list and falling back to normalizing the display name — the same fallback the backend's
 * shopping-list generation uses.
 */
export function normalizedNameOf(ingredient: RecipeIngredient): string {
    return normalizeIngredientName(ingredient.normalizedIngredientName || ingredient.name || '');
}

/**
 * Reduces a plural word to its singular form using the handful of rules recipe words need.
 */
export function singularize(word: string): string {
    const irregular = IRREGULAR_SINGULARS[word];
    if (irregular) {
        return irregular;
    }
    if (word.length <= 3 || !word.endsWith('s')) {
        return word;
    }
    if (word.endsWith('ies') && word.length > 4) {
        return `${word.slice(0, -3)}y`;
    }
    if (word.endsWith('oes')) {
        return word.slice(0, -2);
    }
    if (/(ch|sh|s|x|z)es$/.test(word)) {
        return word.slice(0, -2);
    }
    if (word.endsWith('ss') || word.endsWith('us') || word.endsWith('is')) {
        return word;
    }
    return word.slice(0, -1);
}

/** True for words that describe an amount or a preparation rather than an ingredient. */
export function isNoiseWord(word: string): boolean {
    return FILLER_WORDS.has(word) || QUALIFIER_WORDS.has(word) || UNIT_WORDS.has(word) || /^\d+$/.test(word);
}

/**
 * Turns an ingredient name into the singularized words that identify it, dropping quantities,
 * units, function words and preparation qualifiers. If every word is noise (e.g. "ground cloves",
 * where "cloves" is the ingredient rather than a unit) the full word list is kept instead.
 */
export function toIngredientMatchTokens(name: string): string[] {
    const words = normalizeIngredientName(name).split(' ').filter(Boolean);
    if (words.length === 0) {
        return [];
    }

    const significant = words.filter(word => !isNoiseWord(word));
    const chosen = significant.length > 0 ? significant : words;
    return chosen.map(singularize);
}
