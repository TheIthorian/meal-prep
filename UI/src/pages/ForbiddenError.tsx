import { Button } from '@/components/ui/button';
import { useNavigate } from 'react-router-dom';
import { Home, ShieldAlert } from 'lucide-react';

export default function ForbiddenError() {
    const navigate = useNavigate();

    return (
        <div className='flex min-h-screen items-center justify-center bg-background px-4'>
            <div className='text-center space-y-6 animate-fade-in'>
                {/* Illustration placeholder */}
                <div className='relative mx-auto w-64 h-64 mb-8'>
                    <div className='absolute inset-0 flex items-center justify-center'>
                        <div className='relative'>
                            <ShieldAlert className='h-32 w-32 text-muted-foreground/20' />
                            <div className='absolute -right-4 -top-4 rounded-full bg-warning/10 p-4'>
                                <span className='text-6xl font-bold text-warning'>403</span>
                            </div>
                        </div>
                    </div>
                </div>

                <div className='space-y-2'>
                    <h1 className='text-4xl font-bold tracking-tight'>Access Denied</h1>
                    <p className='text-lg text-muted-foreground max-w-md mx-auto'>
                        You don't have permission to access this resource. Please contact your workspace administrator.
                    </p>
                </div>

                <div className='flex gap-4 justify-center'>
                    <Button onClick={() => navigate(-1)} variant='outline'>
                        Go Back
                    </Button>
                    <Button onClick={() => navigate('/')}>
                        <Home className='mr-2 h-4 w-4' />
                        Return Home
                    </Button>
                </div>
            </div>
        </div>
    );
}
