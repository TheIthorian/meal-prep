import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useAuth } from '@/contexts/AuthContext';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { authApi } from '@/lib/api';
import { toast } from '@/hooks/use-toast';
import * as z from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import {
    AlertDialog,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AxiosError } from 'axios';
import { HttpValidationProblemDetails } from '@/models/error';

const profileSchema = z.object({
    displayName: z.string().min(1, 'Display name is required').max(100, 'Display name must not exceed 100 characters'),
});

export function ProfileSettings() {
    const { user, clearCurrentUser } = useAuth();
    const queryClient = useQueryClient();
    const navigate = useNavigate();
    const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
    const [deleteConfirmation, setDeleteConfirmation] = useState('');

    const profileForm = useForm<z.infer<typeof profileSchema>>({
        resolver: zodResolver(profileSchema),
        defaultValues: {
            displayName: user?.displayName || '',
        },
    });

    // Update form when user data changes
    useEffect(() => {
        if (user?.displayName) {
            profileForm.reset({ displayName: user.displayName });
        }
    }, [user?.displayName, profileForm]);

    const updateProfile = useMutation({
        mutationFn: (values: { displayName: string }) => authApi.updateMe(values),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['user'] });
            toast({
                title: 'Profile updated',
                description: 'Your profile has been successfully updated.',
            });
        },
    });

    const deleteAccount = useMutation({
        mutationFn: () => authApi.deleteMe(),
        onSuccess: async () => {
            toast({
                title: 'Account deleted',
                description: 'Your account has been successfully deleted.',
            });
            clearCurrentUser();
            navigate('/login');
        },
        onError: (error: AxiosError<HttpValidationProblemDetails>) => {
            toast({
                variant: 'destructive',
                title: 'Error',
                description: error.response?.data?.detail || 'Failed to delete account. Please try again.',
            });
            setIsDeleteModalOpen(false);
        },
    });

    const onProfileSubmit = (values: { displayName: string }) => {
        updateProfile.mutate(values);
    };

    return (
        <>
            <div className='space-y-6'>
                <Card className='overflow-hidden border-border/80 shadow-sm'>
                    <CardHeader className='border-b border-border/60 bg-muted/20'>
                        <CardTitle className='text-lg'>Profile</CardTitle>
                        <CardDescription>Your display name and sign-in email</CardDescription>
                    </CardHeader>
                    <CardContent className='space-y-4 pt-6'>
                        <Form {...profileForm}>
                            <form onSubmit={profileForm.handleSubmit(onProfileSubmit)} className='space-y-6'>
                                <div className='grid gap-6 sm:grid-cols-2'>
                                    <FormField
                                        control={profileForm.control}
                                        name='displayName'
                                        render={({ field }) => (
                                            <FormItem>
                                                <FormLabel>Display name</FormLabel>
                                                <FormControl>
                                                    <Input autoComplete='name' {...field} />
                                                </FormControl>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />
                                    <FormItem>
                                        <FormLabel htmlFor='email'>Email</FormLabel>
                                        <Input
                                            id='email'
                                            type='email'
                                            autoComplete='email'
                                            defaultValue={user?.email}
                                            disabled
                                            className='bg-muted/50'
                                        />
                                        <p className='text-xs text-muted-foreground'>Email cannot be changed here.</p>
                                    </FormItem>
                                </div>
                                <Button type='submit' disabled={updateProfile.isPending}>
                                    {updateProfile.isPending ? 'Saving…' : 'Save profile'}
                                </Button>
                            </form>
                        </Form>
                    </CardContent>
                </Card>

                <Card className='overflow-hidden border-destructive/30 shadow-sm'>
                    <CardHeader className='border-b border-destructive/20 bg-destructive/5'>
                        <CardTitle className='text-lg text-destructive'>Danger zone</CardTitle>
                        <CardDescription>Deleting your account removes workspaces and recipes you own.</CardDescription>
                    </CardHeader>
                    <CardContent className='pt-6'>
                        <div className='flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between'>
                            <div className='space-y-1'>
                                <p className='font-medium'>Delete account</p>
                                <p className='text-sm text-muted-foreground'>
                                    This cannot be undone. All data tied to your account will be removed.
                                </p>
                            </div>
                            <Button
                                variant='destructive'
                                className='shrink-0 sm:ml-4'
                                onClick={() => setIsDeleteModalOpen(true)}
                            >
                                Delete account
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            </div>

            <AlertDialog open={isDeleteModalOpen} onOpenChange={setIsDeleteModalOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Are you absolutely sure?</AlertDialogTitle>
                        <AlertDialogDescription>
                            This action cannot be undone. This will permanently delete your account and remove your data
                            from our servers.
                            {user?.email && (
                                <div className='mt-2 p-2 bg-muted rounded text-sm'>
                                    Please confirm by typing <strong>delete my account</strong> below.
                                </div>
                            )}
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <div className='my-2'>
                        <Input
                            value={deleteConfirmation}
                            onChange={e => setDeleteConfirmation(e.target.value)}
                            placeholder='delete my account'
                        />
                    </div>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <Button
                            variant='destructive'
                            onClick={() => deleteAccount.mutate()}
                            disabled={deleteConfirmation !== 'delete my account' || deleteAccount.isPending}
                        >
                            {deleteAccount.isPending ? 'Deleting...' : 'Delete Account'}
                        </Button>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </>
    );
}
