import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { buildAuthPath } from '@/lib/return-url';

interface ShareSignupPromptProps {
    /** Where the visitor lands after signing up or signing in. */
    returnUrl: string;
    /** The offer, in the visitor's terms. Kept to one short sentence — this bar is always on screen. */
    headline: string;
    /** What an account gets them, after the headline. */
    detail: string;
}

/**
 * The signup prompt shown to a signed-out visitor on a share link.
 *
 * A single element rather than one per breakpoint, so the page never carries a duplicate pair of signup
 * links for screen readers and keyboard users. Render it first inside the page container: it then reads
 * and tabs ahead of the shared content, sticks to the top of the page on a wide screen, and anchors to
 * the bottom of the viewport on a phone, where the thumb is and where it costs no reading height.
 *
 * The page that renders it owes its own bottom padding on small screens so the last of the content
 * clears the bar.
 */
export function ShareSignupPrompt({ returnUrl, headline, detail }: ShareSignupPromptProps) {
    return (
        // Opaque rather than translucent: the bar floats over recipe cards of a very similar tone, and at
        // any transparency the content behind it shows through and the edge disappears. Elevation carries
        // the separation instead.
        <div className='fixed inset-x-0 bottom-0 z-40 border-t border-border bg-card shadow-[0_-8px_24px_-12px_rgba(0,0,0,0.7)] lg:sticky lg:inset-x-auto lg:bottom-auto lg:top-4 lg:rounded-xl lg:border lg:shadow-[0_12px_32px_-12px_rgba(0,0,0,0.8)]'>
            <div className='flex items-center justify-between gap-3 px-4 py-3 lg:px-5'>
                <p className='hidden text-sm text-muted-foreground sm:block'>
                    <span className='font-medium text-foreground'>{headline}</span> {detail}
                </p>
                <div className='flex flex-1 gap-2 sm:flex-none'>
                    <Button asChild size='sm' className='flex-1 sm:flex-none'>
                        <Link to={buildAuthPath('/register', returnUrl)}>Create free account</Link>
                    </Button>
                    <Button asChild size='sm' variant='outline' className='flex-1 sm:flex-none'>
                        <Link to={buildAuthPath('/login', returnUrl)}>Sign in</Link>
                    </Button>
                </div>
            </div>
        </div>
    );
}
