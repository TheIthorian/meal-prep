import { Loader2 } from 'lucide-react';

export function LoadingState({ label = 'Loading...' }: { label?: string }) {
    return (
        <div className='flex min-h-[240px] items-center justify-center rounded-xl border border-dashed border-border bg-card/40'>
            <div className='flex items-center gap-3 text-muted-foreground'>
                <Loader2 className='h-5 w-5 animate-spin' />
                <span>{label}</span>
            </div>
        </div>
    );
}
