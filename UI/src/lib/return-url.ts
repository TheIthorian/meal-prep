/**
 * Return URLs survive the sign-up / sign-in round trip as a `returnUrl` query parameter, so a
 * visitor who opens a share link lands back on that link once they have an account.
 *
 * Anything that reaches these helpers is attacker controlled, so a candidate is only accepted when
 * it is a local path matching the allowlist below. Absolute, protocol-relative and encoded escapes
 * are rejected to rule out open redirects.
 */
const allowedPathPrefixes = ['/share/recipe-collections/', '/share/recipes/', '/workspaces/'] as const;
const allowedExactPaths = ['/', '/settings', '/help'] as const;
// eslint-disable-next-line no-control-regex
const controlCharacters = /[\u0000-\u001f\u007f]/;
const schemePrefix = /^[a-z][a-z0-9+.-]*:/i;

export const returnUrlParamName = 'returnUrl';

export function sanitizeReturnUrl(candidate?: string | null): string | undefined {
    if (typeof candidate !== 'string') return undefined;

    const value = candidate.trim();
    if (value.length === 0) return undefined;

    // Reject control characters (URL / header splitting) before any other parsing.
    if (controlCharacters.test(value)) return undefined;

    // Must be a path on this origin: exactly one leading slash and no scheme.
    if (!value.startsWith('/')) return undefined;
    if (value.startsWith('//') || value.startsWith('/\\')) return undefined;

    // Reject anything that still looks like another origin once decoded.
    const decoded = tryDecode(value);
    if (decoded === undefined) return undefined;
    if (decoded.startsWith('//') || decoded.startsWith('/\\') || decoded.startsWith('\\')) return undefined;
    if (schemePrefix.test(decoded)) return undefined;

    const pathname = value.split(/[?#]/)[0];
    const isAllowed =
        allowedExactPaths.some(path => pathname === path) ||
        allowedPathPrefixes.some(prefix => pathname.startsWith(prefix));

    return isAllowed ? value : undefined;
}

export function readReturnUrl(search: string): string | undefined {
    const params = new URLSearchParams(search);
    return sanitizeReturnUrl(params.get(returnUrlParamName));
}

export function buildAuthPath(authPath: string, returnUrl?: string | null): string {
    const safeReturnUrl = sanitizeReturnUrl(returnUrl);
    if (!safeReturnUrl) return authPath;

    return `${authPath}?${returnUrlParamName}=${encodeURIComponent(safeReturnUrl)}`;
}

function tryDecode(value: string): string | undefined {
    try {
        return decodeURIComponent(value);
    } catch {
        return undefined;
    }
}
