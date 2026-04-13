import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';

export default function TermsOfService() {
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
                        <CardTitle className='text-3xl'>Terms of Service</CardTitle>
                        <p className='text-muted-foreground'>Last updated: January 2025</p>
                    </CardHeader>
                    <CardContent className='prose prose-sm max-w-none space-y-6'>
                        <section>
                            <h2 className='text-xl font-semibold mb-3'>1. Acceptance of Terms</h2>
                            <p className='text-muted-foreground'>
                                By accessing and using Meal Prep, you accept and agree to be bound by the terms and
                                provision of this agreement.
                            </p>
                        </section>

                        <section>
                            <h2 className='text-xl font-semibold mb-3'>2. Use License</h2>
                            <p className='text-muted-foreground'>
                                Permission is granted to use Meal Prep for personal and internal planning purposes. This
                                is the grant of a license, not a transfer of title.
                            </p>
                        </section>

                        <section>
                            <h2 className='text-xl font-semibold mb-3'>3. User Account</h2>
                            <p className='text-muted-foreground'>
                                You are responsible for maintaining the confidentiality of your account and password.
                                You agree to accept responsibility for all activities that occur under your account.
                            </p>
                        </section>

                        <section>
                            <h2 className='text-xl font-semibold mb-3'>4. Data Security</h2>
                            <p className='text-muted-foreground'>
                                We implement appropriate security measures to protect your account and application data.
                                However, no method of transmission over the Internet is 100% secure.
                            </p>
                        </section>

                        <section>
                            <h2 className='text-xl font-semibold mb-3'>5. Service Modifications</h2>
                            <p className='text-muted-foreground'>
                                We reserve the right to modify or discontinue the service at any time without notice. We
                                shall not be liable to you or any third party for any modification or discontinuation.
                            </p>
                        </section>

                        <p className='text-sm text-muted-foreground pt-4'>
                            This is a placeholder document and will be updated with full terms of service.
                        </p>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
