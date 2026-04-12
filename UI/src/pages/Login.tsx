import { useState } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ChefHat, Loader2 } from 'lucide-react';
import { analyticsEvents, useAnalytics } from '@/lib/analytics';

type LocationState = { from?: { pathname?: string } };

export default function Login() {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [isLoading, setLoading] = useState(false);
    const { login } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const { capture } = useAnalytics();

    const from = (location.state as LocationState)?.from?.pathname || '/';

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true);

        try {
            await login(email, password);
            capture(analyticsEvents.userLoggedIn, { destination_path: from });
            navigate(from, { replace: true });
        } catch (error) {
            // Error handled by http client
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
                    <p className='mt-2 text-muted-foreground'>Turn recipes into shopping lists</p>
                </div>

                <Card>
                    <CardHeader>
                        <CardTitle>Sign In</CardTitle>
                        <CardDescription>Enter your credentials to access your account</CardDescription>
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
                                <Label htmlFor='password'>Password</Label>
                                <Input
                                    id='password'
                                    type='password'
                                    placeholder='••••••••'
                                    value={password}
                                    onChange={e => setPassword(e.target.value)}
                                    required
                                    disabled={isLoading}
                                />
                            </div>

                            <Button type='submit' className='w-full' disabled={isLoading}>
                                {isLoading ? (
                                    <>
                                        <Loader2 className='mr-2 h-4 w-4 animate-spin' />
                                        Signing in...
                                    </>
                                ) : (
                                    'Sign In'
                                )}
                            </Button>

                            <p className='text-center text-sm text-muted-foreground'>
                                Don't have an account?{' '}
                                <Link to='/register' className='font-medium text-primary hover:underline'>
                                    Register
                                </Link>
                            </p>
                        </form>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
