import { Loader2 } from 'lucide-react';

/**
 * Full-viewport loading indicator, used while an auth check resolves or a lazily
 * loaded route chunk is being fetched.
 */
export function FullPageSpinner() {
    return (
        <div className='flex min-h-screen items-center justify-center bg-background'>
            <Loader2 className='h-8 w-8 animate-spin text-primary' />
        </div>
    );
}
