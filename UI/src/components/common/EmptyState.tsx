import { Button } from '@/components/ui/button';

interface EmptyStateProps {
    title: string;
    description: string;
    actionLabel?: string;
    onAction?: () => void;
}

export function EmptyState({ title, description, actionLabel, onAction }: EmptyStateProps) {
    return (
        <div className='rounded-2xl border border-dashed border-border bg-card p-8 text-center'>
            <h3 className='text-lg font-semibold tracking-tight'>{title}</h3>
            <p className='mt-2 text-sm text-muted-foreground'>{description}</p>
            {actionLabel && onAction && (
                <Button className='mt-4' onClick={onAction}>
                    {actionLabel}
                </Button>
            )}
        </div>
    );
}
