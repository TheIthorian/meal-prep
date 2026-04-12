import { ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Link } from 'react-router-dom';

interface HelpArticleLayoutProps {
    eyebrow: string;
    title: string;
    description: string;
    children: ReactNode;
}

export function HelpArticleLayout({ eyebrow, title, description, children }: HelpArticleLayoutProps) {
    return (
        <div className='mx-auto max-w-4xl space-y-8 px-4 py-8 animate-fade-in'>
            <div className='space-y-3'>
                <div className='flex flex-wrap gap-2'>
                    <Button asChild variant='link' size='sm' className='h-auto px-0 py-0'>
                        <Link to='/'>Back to App</Link>
                    </Button>
                    <Button asChild variant='link' size='sm' className='h-auto px-0 py-0'>
                        <Link to='/help'>Back to Help Center</Link>
                    </Button>
                </div>
                <div className='space-y-2'>
                    <p className='text-sm font-medium uppercase tracking-[0.18em] text-muted-foreground'>{eyebrow}</p>
                    <div>
                        <h1 className='text-2xl font-bold tracking-tight sm:text-3xl'>{title}</h1>
                        <p className='mt-2 max-w-2xl text-muted-foreground'>{description}</p>
                    </div>
                </div>
            </div>

            <div className='space-y-6'>{children}</div>
        </div>
    );
}
