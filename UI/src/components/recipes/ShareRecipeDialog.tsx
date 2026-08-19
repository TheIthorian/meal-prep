import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Check, X } from 'lucide-react';
import { recipeSharesApi } from '@/lib/api';
import { toast } from '@/hooks/use-toast';
import { Button } from '@/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';

interface ShareRecipeDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    workspaceId: string;
    recipeId: string;
    recipeTitle: string;
}

/** What a recipient of the link receives, and what stays behind. Kept next to the link itself so the
 *  sharer can see it before they send anything. */
const sharedFields = [
    'Title, description and photo',
    'Servings, prep and cook time',
    'Ingredients, method and nutrition',
    'Tags and the original source link',
    'Your recipe notes',
];

const privateFields = ['Your meal plan and shopping lists', 'Your collections and favourites', 'Your workspace name'];

export function ShareRecipeDialog({ open, onOpenChange, workspaceId, recipeId, recipeTitle }: ShareRecipeDialogProps) {
    const [shareUrl, setShareUrl] = useState<string | null>(null);

    const createShareLink = useMutation({
        mutationFn: () => recipeSharesApi.createShareLink(workspaceId, recipeId),
        onSuccess: async data => {
            const absoluteUrl = `${window.location.origin}${data.sharePath}`;
            setShareUrl(absoluteUrl);
            try {
                if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
                    await navigator.clipboard.writeText(absoluteUrl);
                    toast({ title: 'Share link copied' });
                    return;
                }
            } catch {
                // fall through to the link-visible fallback toast
            }
            toast({
                title: 'Share link ready',
                description: 'Clipboard is unavailable here. Copy the link from this dialog.',
            });
        },
        onError: () => {
            toast({ title: 'Could not create a share link', variant: 'destructive' });
        },
    });

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Share this recipe</DialogTitle>
                    <DialogDescription>
                        Anyone with the link can read &quot;{recipeTitle}&quot; and cook from it. They cannot change
                        your copy. To make it their own, they save a copy into their own recipes.
                    </DialogDescription>
                </DialogHeader>

                <div className='grid gap-4 sm:grid-cols-2'>
                    <section>
                        <h3 className='text-sm font-medium text-foreground'>They will see</h3>
                        <ul className='mt-2 space-y-1.5'>
                            {sharedFields.map(field => (
                                <li key={field} className='flex items-start gap-2 text-sm text-muted-foreground'>
                                    <Check className='mt-0.5 h-4 w-4 shrink-0 text-primary' aria-hidden />
                                    {field}
                                </li>
                            ))}
                        </ul>
                    </section>

                    <section>
                        <h3 className='text-sm font-medium text-foreground'>They will not see</h3>
                        <ul className='mt-2 space-y-1.5'>
                            {privateFields.map(field => (
                                <li key={field} className='flex items-start gap-2 text-sm text-muted-foreground'>
                                    <X className='mt-0.5 h-4 w-4 shrink-0' aria-hidden />
                                    {field}
                                </li>
                            ))}
                        </ul>
                    </section>
                </div>

                {shareUrl ? (
                    <div className='rounded-md border border-border bg-muted/30 p-3 text-sm text-muted-foreground break-all'>
                        {shareUrl}
                    </div>
                ) : null}

                <DialogFooter>
                    <Button
                        type='button'
                        onClick={() => void createShareLink.mutateAsync()}
                        disabled={createShareLink.isPending}
                    >
                        {shareUrl ? 'Copy link again' : 'Create and copy link'}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
