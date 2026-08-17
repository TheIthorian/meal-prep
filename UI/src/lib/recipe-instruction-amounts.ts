import type { RecipeIngredient } from '@/models/meal-prep';
import { formatAmount } from '@/lib/meal-prep';
import {
    isNoiseWord,
    normalizeIngredientName,
    normalizedNameOf,
    singularize,
    toIngredientMatchTokens,
} from '@/lib/ingredient-normalization';

export type InstructionSegment = { kind: 'text'; text: string } | { kind: 'bracket'; bracket: string; key: string };

export interface InstructionIngredientMatch {
    /** Index of the first character of the matched words in the instruction. */
    start: number;
    /** Index just past the last character of the matched words. */
    end: number;
    ingredient: RecipeIngredient;
}

/** How many qualifier/filler words may sit between two words of an ingredient name. */
const MAX_INTERVENING_WORDS = 2;

interface InstructionWord {
    start: number;
    end: number;
    /** Singularized, accent-folded form used for comparison. */
    value: string;
    /** True when the word is a quantity, unit, function word or preparation qualifier. */
    isNoise: boolean;
}

interface Candidate {
    tokens: string[];
    ingredient: RecipeIngredient;
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

function splitInstructionIntoWords(instruction: string): InstructionWord[] {
    const words: InstructionWord[] = [];
    const wordPattern = /[\p{L}\p{N}]+/gu;
    let match: RegExpExecArray | null;

    while ((match = wordPattern.exec(instruction)) !== null) {
        const normalized = normalizeIngredientName(match[0]);
        if (!normalized) {
            continue;
        }
        const value = singularize(normalized);
        words.push({
            start: match.index,
            end: match.index + match[0].length,
            value,
            isNoise: isNoiseWord(normalized) || isNoiseWord(value),
        });
    }

    return words;
}

/**
 * Builds the phrases each ingredient can be recognised by. An ingredient joining two things
 * ("salt and pepper") also contributes each half, so a step mentioning only one still resolves.
 */
function buildCandidates(ingredients: RecipeIngredient[]): Candidate[] {
    const candidates: Candidate[] = [];

    for (const ingredient of ingredients) {
        const normalized = normalizedNameOf(ingredient);
        if (!normalized) {
            continue;
        }

        const phrases = [normalized, ...normalized.split(/\band\b/g)];
        const seen = new Set<string>();

        for (const phrase of phrases) {
            const tokens = toIngredientMatchTokens(phrase);
            if (tokens.length === 0) {
                continue;
            }
            const key = tokens.join(' ');
            if (seen.has(key)) {
                continue;
            }
            seen.add(key);
            candidates.push({ tokens, ingredient });
        }
    }

    return dropAmbiguousCandidates(candidates).sort(
        (a, b) => b.tokens.length - a.tokens.length || b.tokens.join(' ').length - a.tokens.join(' ').length,
    );
}

/**
 * Removes phrases that more than one ingredient answers to. A step saying "the butter" when the
 * list holds two butter rows is genuinely ambiguous, so it is left unmatched rather than guessed.
 */
function dropAmbiguousCandidates(candidates: Candidate[]): Candidate[] {
    const owners = new Map<string, Set<RecipeIngredient>>();

    for (const candidate of candidates) {
        const key = candidate.tokens.join(' ');
        const existing = owners.get(key) ?? new Set<RecipeIngredient>();
        existing.add(candidate.ingredient);
        owners.set(key, existing);
    }

    return candidates.filter(candidate => owners.get(candidate.tokens.join(' '))?.size === 1);
}

/**
 * Tries to match every word of `tokens` against consecutive instruction words starting at
 * `startIndex`, allowing a couple of qualifier words in between ("chicken, finely sliced thighs").
 * Returns the index of the last instruction word consumed, or null when the phrase does not match.
 */
function matchTokensAt(words: InstructionWord[], startIndex: number, tokens: string[]): number | null {
    if (words[startIndex].value !== tokens[0]) {
        return null;
    }

    let wordIndex = startIndex;

    for (let tokenIndex = 1; tokenIndex < tokens.length; tokenIndex++) {
        let skipped = 0;
        let next = wordIndex + 1;

        while (next < words.length && words[next].isNoise && words[next].value !== tokens[tokenIndex]) {
            if (skipped >= MAX_INTERVENING_WORDS) {
                return null;
            }
            skipped++;
            next++;
        }

        if (next >= words.length || words[next].value !== tokens[tokenIndex]) {
            return null;
        }
        wordIndex = next;
    }

    return wordIndex;
}

/**
 * Finds the ingredients a step refers to, matching on normalized names rather than raw strings so
 * plurals, preparation words and leading quantities in the prose still resolve. Longer names are
 * matched first so "spring onion" is not claimed by "onion", and phrases that several ingredients
 * share stay unmatched. Results are ordered by position in the instruction.
 */
export function findInstructionIngredientMatches(
    instruction: string,
    ingredients: RecipeIngredient[],
): InstructionIngredientMatch[] {
    const words = splitInstructionIntoWords(instruction);
    if (words.length === 0) {
        return [];
    }

    const matches: InstructionIngredientMatch[] = [];
    const claimedWords = new Set<number>();

    for (const candidate of buildCandidates(ingredients)) {
        for (let index = 0; index < words.length; index++) {
            if (claimedWords.has(index)) {
                continue;
            }

            const lastIndex = matchTokensAt(words, index, candidate.tokens);
            if (lastIndex === null) {
                continue;
            }

            let overlaps = false;
            for (let claimed = index; claimed <= lastIndex; claimed++) {
                if (claimedWords.has(claimed)) {
                    overlaps = true;
                    break;
                }
            }
            if (overlaps) {
                continue;
            }

            for (let claimed = index; claimed <= lastIndex; claimed++) {
                claimedWords.add(claimed);
            }
            matches.push({ start: words[index].start, end: words[lastIndex].end, ingredient: candidate.ingredient });
            index = lastIndex;
        }
    }

    return matches.sort((a, b) => a.start - b.start);
}

/**
 * Finds ingredient names in prose and builds segments: plain text plus optional bracket chunks
 * with scaled amounts after each matched name.
 */
export function buildInstructionSegments(instruction: string, ingredients: RecipeIngredient[]): InstructionSegment[] {
    const matches = findInstructionIngredientMatches(instruction, ingredients);

    const segments: InstructionSegment[] = [];
    let cursor = 0;
    let bracketKey = 0;

    for (const match of matches) {
        if (match.start > cursor) {
            segments.push({ kind: 'text', text: instruction.slice(cursor, match.start) });
        }
        segments.push({ kind: 'text', text: instruction.slice(match.start, match.end) });
        const bracket = formatIngredientAmountBracket(match.ingredient);
        if (bracket) {
            segments.push({ kind: 'bracket', bracket, key: `ing-bracket-${bracketKey++}-${match.start}` });
        }
        cursor = match.end;
    }

    if (cursor < instruction.length) {
        segments.push({ kind: 'text', text: instruction.slice(cursor) });
    }

    return segments;
}
