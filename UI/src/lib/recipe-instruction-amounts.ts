import type { RecipeIngredient } from '@/models/meal-prep';
import { formatAmount } from '@/lib/meal-prep';

export type InstructionSegment =
    | { kind: 'text'; text: string }
    | { kind: 'bracket'; bracket: string; key: string };

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Returns a compact amount string for inline instruction annotations, e.g. "1 tbsp" or "2".
 */
export function formatIngredientAmountBracket(ingredient: RecipeIngredient): string | null {
    const { amount, unit } = ingredient;
    if (amount === null || amount === undefined || Number.isNaN(amount)) {
        return null;
    }
    const a = formatAmount(amount);
    const u = unit?.trim();
    if (u) {
        return `[${a} ${u}]`;
    }
    return `[${a}]`;
}

/**
 * Finds ingredient names in prose (longest names first) and builds segments: plain text plus
 * optional bracket chunks with scaled amounts after each matched name.
 */
export function buildInstructionSegments(instruction: string, ingredients: RecipeIngredient[]): InstructionSegment[] {
    const trimmed = ingredients.filter(i => i.name.trim().length > 0);
    const sortedByNameLength = [...trimmed].sort((a, b) => b.name.trim().length - a.name.trim().length);

    type Claim = { start: number; end: number; ingredient: RecipeIngredient };
    const claimed: Claim[] = [];

    function overlaps(start: number, end: number): boolean {
        return claimed.some(c => !(end <= c.start || start >= c.end));
    }

    for (const ingredient of sortedByNameLength) {
        const pattern = ingredient.name.trim();
        const re = new RegExp(`\\b${escapeRegExp(pattern)}\\b`, 'gi');
        let match: RegExpExecArray | null;
        while ((match = re.exec(instruction)) !== null) {
            const start = match.index;
            const end = start + match[0].length;
            if (overlaps(start, end)) {
                continue;
            }
            claimed.push({ start, end, ingredient });
        }
    }

    claimed.sort((a, b) => a.start - b.start);

    const segments: InstructionSegment[] = [];
    let cursor = 0;
    let bracketKey = 0;

    for (const c of claimed) {
        if (c.start > cursor) {
            segments.push({ kind: 'text', text: instruction.slice(cursor, c.start) });
        }
        segments.push({ kind: 'text', text: instruction.slice(c.start, c.end) });
        const bracket = formatIngredientAmountBracket(c.ingredient);
        if (bracket) {
            segments.push({ kind: 'bracket', bracket, key: `ing-bracket-${bracketKey++}-${c.start}` });
        }
        cursor = c.end;
    }

    if (cursor < instruction.length) {
        segments.push({ kind: 'text', text: instruction.slice(cursor) });
    }

    return segments;
}
