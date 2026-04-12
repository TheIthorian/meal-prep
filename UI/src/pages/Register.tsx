import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ChefHat, Loader2 } from 'lucide-react';
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics } from '@/lib/analytics';

export default function Register() {
    const [email, setEmail] = useState('');
    const [displayName, setDisplayName] = useState('');
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [isLoading, setLoading] = useState(false);
    const { register } = useAuth();
    const navigate = useNavigate();
    const { capture } = useAnalytics();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        if (password !== confirmPassword) {
            toast({
                title: 'Passwords do not match',
                description: 'Please ensure your passwords match',
                variant: 'destructive',
            });
            return;
        }

        setLoading(true);

        try {
            await register(email, password, displayName || undefined);
            capture(analyticsEvents.userRegistered, { has_display_name: !!displayName.trim() });
            toast({
                title: 'Success',
                description: 'Your account has been created. Please log in.',
            });
            navigate('/login', { replace: true });
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className='flex min-h-screen items-center justify-center bg-background px-4'>
            <div className='w-full max-w-md space-y-8 animate-fade-in'>
                <div className='text-center'>
                    <div className='flex justify-center mb-4'>
                        <div className='rounded-full bg-primary p-3'>
                            <ChefHat className='h-8 w-8 text-primary-foreground' aria-hidden />
                        </div>
                    </div>
                    <h1 className='text-3xl font-bold tracking-tight'>Meal Prep</h1>
                    <p className='mt-2 text-muted-foreground'>Create your account to start planning recipes</p>
                </div>

                <Card>
                    <CardHeader>
                        <CardTitle>Register</CardTitle>
                        <CardDescription>Create a new account to get started</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <form onSubmit={handleSubmit} className='space-y-4'>
                            <div className='space-y-2'>
                                <Label htmlFor='email'>Email</Label>
                                <Input
                                    id='email'
                                    type='email'
                                    placeholder='you@example.com'
                                    value={email}
                                    onChange={e => setEmail(e.target.value)}
                                    required
                                    disabled={isLoading}
                                />
                            </div>

                            <div className='space-y-2'>
                                <Label htmlFor='displayName'>Display Name (Optional)</Label>
                                <Input
                                    id='displayName'
                                    type='text'
                                    placeholder='johndoe'
                                    value={displayName}
                                    onChange={e => setDisplayName(e.target.value)}
                                    disabled={isLoading}
                                />
                            </div>

                            <div className='space-y-2'>
                                <Label htmlFor='password'>Password</Label>
                                <Input
                                    id='password'
                                    type='password'
                                    placeholder='••••••••'
                                    value={password}
                                    onChange={e => setPassword(e.target.value)}
                                    required
                                    disabled={isLoading}
                                    minLength={8}
                                />
                            </div>

                            <div className='space-y-2'>
                                <Label htmlFor='confirmPassword'>Confirm Password</Label>
                                <Input
                                    id='confirmPassword'
                                    type='password'
                                    placeholder='••••••••'
                                    value={confirmPassword}
                                    onChange={e => setConfirmPassword(e.target.value)}
                                    required
                                    disabled={isLoading}
                                    minLength={8}
                                />
                            </div>

                            <Button type='submit' className='w-full' disabled={isLoading}>
                                {isLoading ? (
                                    <>
                                        <Loader2 className='mr-2 h-4 w-4 animate-spin' />
                                        Creating account...
                                    </>
                                ) : (
                                    'Create Account'
                                )}
                            </Button>

                            <p className='text-center text-sm text-muted-foreground'>
                                Already have an account?{' '}
                                <Link to='/login' className='font-medium text-primary hover:underline'>
                                    Sign In
                                </Link>
                            </p>
                        </form>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
