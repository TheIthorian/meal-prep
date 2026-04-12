import { useRef } from 'react';

type Timeout = ReturnType<typeof setTimeout>;

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function useDebounce<T extends (...args: any) => void>(cb: T, delay: number): T {
    const timeoutId = useRef<Timeout>();

    const delayedFunction = (...args: Parameters<T>) => {
        if (timeoutId.current) {
            // This check is not strictly necessary
            clearTimeout(timeoutId.current);
        }

        timeoutId.current = setTimeout(() => cb(...args), delay);
    };

    return delayedFunction as T;
}
