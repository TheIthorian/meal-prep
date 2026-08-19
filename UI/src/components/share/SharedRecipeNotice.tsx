import { Link } from 'react-router-dom';
import { ChefHat, Eye } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { buildAuthPath } from '@/lib/return-url';

interface SharedRecipeNoticeProps {
    /** The workspace that shared the recipe, named so the reader knows whose copy this is. */
    ownerWorkspaceName: string;
    /** Where cooking mode lives for this share link. */
    cookingPath: string;
    /** Where a signed-out visitor comes back to after signing in. */
    returnUrl: string;
    isSignedIn: boolean;
    /** Rendered beside Start cooking: the save action, when the page offers one. */
    saveAction?: React.ReactNode;
}

/**
 * Says plainly what a share link is: a read-only copy of someone else's recipe, which the reader can
 * cook from or take their own copy of, but cannot change.
 */
export function SharedRecipeNotice({
    ownerWorkspaceName,
    cookingPath,
    returnUrl,
    isSignedIn,
    saveAction,
}: SharedRecipeNoticeProps) {
    return (
        <div className='mb-6 rounded-lg border border-border bg-muted/40 p-4'>
            <p className='flex items-start gap-2 text-sm text-muted-foreground'>
                <Eye className='mt-0.5 h-4 w-4 shrink-0' aria-hidden />
                <span>
                    Shared with you by <span className='font-medium text-foreground'>{ownerWorkspaceName}</span>. You can
                    read and cook this recipe, but not edit it. Save a copy to make it yours.
                </span>
            </p>

            <div className='mt-4 flex flex-wrap gap-2'>
                {/* Cooking mode needs an account, so a signed-out visitor is sent to sign in and
                    brought back to the recipe they were reading. */}
                <Button asChild size='sm' variant='outline'>
                    <Link to={isSignedIn ? cookingPath : buildAuthPath('/login', returnUrl)}>
                        <ChefHat className='mr-1.5 h-4 w-4' aria-hidden />
                        Start cooking
                    </Link>
                </Button>
                {saveAction}
            </div>
        </div>
    );
}
