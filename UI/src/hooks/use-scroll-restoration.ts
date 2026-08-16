import { useEffect, useLayoutEffect, useRef } from 'react';

const STORAGE_PREFIX = 'scroll-restore:';

/** sessionStorage throws in some privacy modes; a lost scroll position is never worth a crash. */
function readNumber(key: string): number {
    try {
        const raw = window.sessionStorage.getItem(key);
        if (raw === null) return 0;
        const value = Number(raw);
        return Number.isFinite(value) ? value : 0;
    } catch {
        return 0;
    }
}

function writeNumber(key: string, value: number) {
    try {
        window.sessionStorage.setItem(key, String(value));
    } catch {
        /* ignore */
    }
}

/**
 * Remembers the window scroll position for `key` and puts it back the next time the
 * component mounts — e.g. returning to a list after opening one of its items.
 *
 * `ready` should flip to true once the content that gives the page its height has
 * rendered, otherwise the restore lands on a short page and gets clamped near the top.
 */
export function useScrollRestoration(key: string, ready: boolean) {
    const storageKey = `${STORAGE_PREFIX}${key}`;
    const hasRestored = useRef(false);

    useEffect(() => {
        hasRestored.current = false;
    }, [storageKey]);

    useEffect(() => {
        let frame = 0;

        function onScroll() {
            if (frame) return;
            frame = window.requestAnimationFrame(() => {
                frame = 0;
                writeNumber(storageKey, window.scrollY);
            });
        }

        window.addEventListener('scroll', onScroll, { passive: true });
        return () => {
            window.removeEventListener('scroll', onScroll);
            if (frame) window.cancelAnimationFrame(frame);
            writeNumber(storageKey, window.scrollY);
        };
    }, [storageKey]);

    useLayoutEffect(() => {
        if (!ready || hasRestored.current) return;
        hasRestored.current = true;

        const target = readNumber(storageKey);
        if (target <= 0) return;

        // Cards settle over a few frames (images, entrance animations), so the document may
        // still be too short for the target on the first try. Keep nudging until it fits.
        let attempts = 0;
        let frame = 0;

        function attempt() {
            window.scrollTo(0, target);
            attempts += 1;
            if (Math.round(window.scrollY) < target && attempts < 10) {
                frame = window.requestAnimationFrame(attempt);
            }
        }

        attempt();
        return () => {
            if (frame) window.cancelAnimationFrame(frame);
        };
    }, [ready, storageKey]);
}
