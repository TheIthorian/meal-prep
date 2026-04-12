import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { helpArticles } from '@/pages/help/articles';
import { Link } from 'react-router-dom';

export default function Help() {
    return (
        <div className='mx-auto max-w-5xl space-y-6 px-4 py-8 animate-fade-in'>
            <div className='space-y-3'>
                <Button asChild variant='link' size='sm' className='h-auto px-0 py-0'>
                    <Link to='/'>Back to App</Link>
                </Button>
                <h1 className='text-2xl font-bold tracking-tight sm:text-3xl'>Help Center</h1>
                <p className='max-w-2xl text-muted-foreground'>Learn how to set up your workspace.</p>
            </div>

            <div className='grid gap-4 md:grid-cols-2'>
                {helpArticles.map(article => (
                    <Card key={article.slug} className='flex h-full flex-col'>
                        <CardHeader className='space-y-3'>
                            <div className='flex items-center gap-2'>
                                <Badge variant='secondary'>{article.audience}</Badge>
                                <span className='text-xs text-muted-foreground'>{article.readTime}</span>
                            </div>
                            <div className='space-y-1'>
                                <CardTitle>{article.title}</CardTitle>
                                <CardDescription>{article.description}</CardDescription>
                            </div>
                        </CardHeader>
                        <CardContent className='mt-auto'>
                            <Button asChild>
                                <Link to={`/help/${article.slug}`}>Open article</Link>
                            </Button>
                        </CardContent>
                    </Card>
                ))}
            </div>
        </div>
    );
}
