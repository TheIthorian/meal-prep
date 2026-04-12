import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useAuth } from '@/contexts/AuthContext';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { authApi } from '@/lib/api';
import { useToast } from '@/components/ui/use-toast';
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
    const { toast } = useToast();
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
            <Card>
                <CardHeader>
                    <CardTitle>Profile Information</CardTitle>
                    <CardDescription>Update your personal information and email address</CardDescription>
                </CardHeader>
                <CardContent className='space-y-4'>
                    <Form {...profileForm}>
                        <form onSubmit={profileForm.handleSubmit(onProfileSubmit)} className='space-y-4'>
                            <div className='grid gap-4 md:grid-cols-2'>
                                <FormField
                                    control={profileForm.control}
                                    name='displayName'
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Display Name</FormLabel>
                                            <FormControl>
                                                <Input {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                                <FormItem>
                                    <FormLabel htmlFor='email'>Email</FormLabel>
                                    <Input id='email' type='email' defaultValue={user?.email} disabled />
                                </FormItem>
                            </div>
                            <Button type='submit' disabled={updateProfile.isPending}>
                                {updateProfile.isPending ? 'Saving...' : 'Save Changes'}
                            </Button>
                        </form>
                    </Form>
                </CardContent>
            </Card>

            <Card className='border-destructive/50'>
                <CardHeader>
                    <CardTitle className='text-destructive'>Danger Zone</CardTitle>
                    <CardDescription>Irreversible actions for your account</CardDescription>
                </CardHeader>
                <CardContent>
                    <div className='flex items-center justify-between'>
                        <div className='space-y-1'>
                            <h4 className='font-medium'>Delete Account</h4>
                            <p className='text-sm text-muted-foreground'>
                                Permanently delete your account and all associated data. This action cannot be undone.
                            </p>
                        </div>
                        <Button variant='destructive' onClick={() => setIsDeleteModalOpen(true)}>
                            Delete Account
                        </Button>
                    </div>
                </CardContent>
            </Card>

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
