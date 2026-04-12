import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';

export default function DataRetention() {
    return (
        <div className='min-h-screen bg-background p-4 md:p-8'>
            <div className='max-w-4xl mx-auto space-y-6 animate-fade-in'>
                <Link
                    to='/'
                    className='inline-flex items-center text-sm text-muted-foreground hover:text-foreground transition-colors'
                >
                    <ArrowLeft className='mr-2 h-4 w-4' />
                    Back to Home
                </Link>

                <Card>
                    <CardHeader>
                        <CardTitle className='text-3xl'>Data Retention Policy</CardTitle>
                        <p className='text-muted-foreground'>Last updated: January 2025</p>
                    </CardHeader>
                    <CardContent className='prose prose-sm max-w-none space-y-6'></CardContent>
                </Card>
            </div>
        </div>
    );
}
