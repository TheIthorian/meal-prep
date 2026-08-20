import { describe, expect, it } from 'vitest';
import { buildAuthPath, readReturnUrl, sanitizeReturnUrl } from './return-url';

describe('sanitizeReturnUrl', () => {
    it('keeps an allowed local path', () => {
        expect(sanitizeReturnUrl('/share/recipe-collections/abc123')).toBe('/share/recipe-collections/abc123');
    });

    it('keeps a single recipe share path', () => {
        expect(sanitizeReturnUrl('/share/recipes/abc123')).toBe('/share/recipes/abc123');
    });

    it('keeps the query string and hash of an allowed local path', () => {
        expect(sanitizeReturnUrl('/workspaces/123/collections?sort=name#top')).toBe(
            '/workspaces/123/collections?sort=name#top',
        );
    });

    it('rejects an absolute url to another host', () => {
        expect(sanitizeReturnUrl('https://evil.example.com/steal')).toBeUndefined();
    });

    it('rejects a protocol-relative url', () => {
        expect(sanitizeReturnUrl('//evil.example.com/steal')).toBeUndefined();
    });

    it('rejects a backslash-prefixed url that browsers treat as protocol-relative', () => {
        expect(sanitizeReturnUrl('/\\evil.example.com')).toBeUndefined();
        expect(sanitizeReturnUrl('\\\\evil.example.com')).toBeUndefined();
    });

    it('rejects encoded attempts to escape the origin', () => {
        expect(sanitizeReturnUrl('/%2F%2Fevil.example.com')).toBeUndefined();
        expect(sanitizeReturnUrl('%2F%2Fevil.example.com')).toBeUndefined();
    });

    it('rejects javascript and data urls', () => {
        expect(sanitizeReturnUrl('javascript:alert(1)')).toBeUndefined();
        expect(sanitizeReturnUrl('data:text/html,<script>alert(1)</script>')).toBeUndefined();
    });

    it('rejects a local path that is not on the allowlist', () => {
        expect(sanitizeReturnUrl('/not-a-real-route')).toBeUndefined();
    });

    it('rejects the auth pages themselves so login cannot loop', () => {
        expect(sanitizeReturnUrl('/login')).toBeUndefined();
        expect(sanitizeReturnUrl('/register')).toBeUndefined();
    });

    it('rejects empty, relative and non-string values', () => {
        expect(sanitizeReturnUrl('')).toBeUndefined();
        expect(sanitizeReturnUrl('share/recipe-collections/abc')).toBeUndefined();
        expect(sanitizeReturnUrl(undefined)).toBeUndefined();
        expect(sanitizeReturnUrl(null)).toBeUndefined();
        expect(sanitizeReturnUrl(42 as unknown as string)).toBeUndefined();
    });

    it('rejects control characters', () => {
        expect(sanitizeReturnUrl('/help\nSet-Cookie: x=1')).toBeUndefined();
    });
});

describe('readReturnUrl', () => {
    it('reads and sanitizes the returnUrl query parameter', () => {
        expect(readReturnUrl('?returnUrl=%2Fshare%2Frecipe-collections%2Fabc123')).toBe(
            '/share/recipe-collections/abc123',
        );
    });

    it('drops an external returnUrl query parameter', () => {
        expect(readReturnUrl('?returnUrl=https%3A%2F%2Fevil.example.com')).toBeUndefined();
    });

    it('returns undefined when the parameter is absent', () => {
        expect(readReturnUrl('?other=1')).toBeUndefined();
    });
});

describe('buildAuthPath', () => {
    it('appends an encoded returnUrl', () => {
        expect(buildAuthPath('/login', '/share/recipe-collections/abc123')).toBe(
            '/login?returnUrl=%2Fshare%2Frecipe-collections%2Fabc123',
        );
    });

    it('omits a rejected returnUrl', () => {
        expect(buildAuthPath('/register', 'https://evil.example.com')).toBe('/register');
    });

    it('omits a missing returnUrl', () => {
        expect(buildAuthPath('/login', undefined)).toBe('/login');
    });
});
