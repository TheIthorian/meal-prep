/**
 * Splits instruction prose into sentences for per-sentence UI (e.g. cooking mode).
 * Splits on whitespace after ., ?, or ! — imperfect for abbreviations (e.g. "e.g.") but
 * sufficient for typical recipe steps.
 */
export function splitInstructionIntoSentences(text: string): string[] {
    const trimmed = text.trim();
    if (!trimmed) {
        return [];
    }
    const parts = trimmed.split(/(?<=[.!?])\s+/);
    return parts.map(p => p.trim()).filter(Boolean);
}
