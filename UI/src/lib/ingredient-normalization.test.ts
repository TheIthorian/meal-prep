import { describe, expect, it } from 'vitest';
import { normalizeIngredientName, toIngredientMatchTokens } from '@/lib/ingredient-normalization';

describe('normalizeIngredientName', () => {
    it('lower-cases, strips punctuation and collapses whitespace like the shopping-list normalizer', () => {
        expect(normalizeIngredientName('  Extra-Virgin  Olive Oil, cold pressed ')).toBe(
            'extra virgin olive oil cold pressed',
        );
    });

    it('folds accents so imported names match plain prose', () => {
        expect(normalizeIngredientName('Tomato Purée')).toBe('tomato puree');
    });

    it('is idempotent', () => {
        const once = normalizeIngredientName('Crème fraîche (full fat)');
        expect(normalizeIngredientName(once)).toBe(once);
    });
});

describe('toIngredientMatchTokens', () => {
    it('drops quantities, units and preparation qualifiers', () => {
        expect(toIngredientMatchTokens('2 cloves of garlic finely chopped')).toEqual(['garlic']);
    });

    it('reduces plurals to their singular form', () => {
        expect(toIngredientMatchTokens('chicken thighs')).toEqual(['chicken', 'thigh']);
        expect(toIngredientMatchTokens('cherry tomatoes')).toEqual(['cherry', 'tomato']);
    });

    it('keeps the whole name when every word looks like a qualifier or unit', () => {
        expect(toIngredientMatchTokens('ground cloves')).toEqual(['ground', 'clove']);
    });

    it('returns nothing for a blank name', () => {
        expect(toIngredientMatchTokens('   ')).toEqual([]);
    });
});
