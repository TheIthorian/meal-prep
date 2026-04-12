import { useMemo, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';

export function useQueryString<T extends string[]>(supportedProperties: T, persistenceKey?: string) {
    const [urlSearchParams, setUrlSearchParams] = useSearchParams();

    // Load from persistence on mount
    useEffect(() => {
        if (!persistenceKey) return;

        const shouldReset = urlSearchParams.get('filter') === 'none';

        if (shouldReset) {
            localStorage.removeItem(persistenceKey);
            setUrlSearchParams(prev => {
                prev.delete('filter');
                return prev;
            });
            return;
        }

        // Only load if URL params are effectively empty (ignoring non-supported ones if we wanted to be strict, but checking size is a good proxy)
        // Or better: check if any supported property is present.
        const hasSupportedParams = supportedProperties.some(prop => urlSearchParams.has(prop));

        if (!hasSupportedParams) {
            const saved = localStorage.getItem(persistenceKey);
            if (saved) {
                try {
                    const parsed = JSON.parse(saved);
                    setUrlSearchParams(parsed);
                } catch (e) {
                    console.error('Failed to parse saved filters', e);
                }
            }
        }
    }, [persistenceKey]); // Run once on mount (or if key changes)

    // Save to persistence on change
    useEffect(() => {
        if (!persistenceKey) return;

        const paramsToSave: Record<string, string> = {};
        supportedProperties.forEach(prop => {
            const val = urlSearchParams.get(prop);
            if (val) paramsToSave[prop] = val;
        });

        if (Object.keys(paramsToSave).length > 0) {
            localStorage.setItem(persistenceKey, JSON.stringify(paramsToSave));
        } else {
            // If all supported params are cleared, should we clear storage?
            // Maybe not, to keep "last known good state"?
            // But if user explicitly clears filters, we probably want to clear storage.
            // Let's clear it if empty to reflect current state.
            localStorage.removeItem(persistenceKey);
        }
    }, [urlSearchParams, persistenceKey, supportedProperties]);

    const params = useMemo(() => {
        const params = {} as Record<T[number], string | undefined>;
        for (const [key, value] of urlSearchParams) {
            if (isSupportedProperty(supportedProperties, key)) params[key] = value;
        }

        return params;
    }, [supportedProperties, urlSearchParams]);

    function setParam(key: T[number], val: string) {
        setUrlSearchParams(u => {
            u.set(key, val);
            return u;
        });
    }

    function deleteParam(key: T[number]) {
        setUrlSearchParams(u => {
            u.delete(key);
            return u;
        });
    }

    return { setParam, deleteParam, params };
}

function isSupportedProperty<T extends string[]>(supportedProperties: T, key: unknown): key is T[number] {
    return supportedProperties.includes(key as T[number]);
}
