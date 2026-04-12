import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useAuth } from '@/contexts/AuthContext';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { Plus, UserPlus, Trash2, Pencil } from 'lucide-react';
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { workspacesApi } from '@/lib/api';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { useIsMobile } from '@/hooks/use-mobile';
import * as z from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { MemberListItem } from '@/models/workspace';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';

const workspaceSchema = z.object({
    name: z.string().min(1, 'Name is required'),
});

const inviteMemberSchema = z.object({
    email: z.string().email('Invalid email address'),
    role: z.string().min(1, 'Role is required'),
});

export function WorkspaceSettings() {
    const isMobile = useIsMobile();
    const { user } = useAuth();
    const { currentWorkspace } = useWorkspace();
    const queryClient = useQueryClient();
    const { toast } = useToast();
    const { capture } = useAnalytics();

    const [isAddWorkspaceModalOpen, setIsAddWorkspaceModalOpen] = useState(false);
    const [isInviteMemberModalOpen, setIsInviteMemberModalOpen] = useState(false);
    const [isEditWorkspaceModalOpen, setIsEditWorkspaceModalOpen] = useState(false);
    const [isDeleteWorkspaceModalOpen, setIsDeleteWorkspaceModalOpen] = useState(false);
    const [workspaceToEdit, setWorkspaceToEdit] = useState<{ id: string; name: string; isOwner: boolean } | null>(null);
    const [deleteConfirmation, setDeleteConfirmation] = useState('');

    const { data: workspaces } = useQuery({
        queryKey: ['workspaces'],
        queryFn: () => workspacesApi.getAll(),
        initialData: [],
    });

    const members = workspaces.find(workspace => workspace.id === currentWorkspace.workspaceId)?.members || [];

    const form = useForm<z.infer<typeof workspaceSchema>>({
        resolver: zodResolver(workspaceSchema),
        defaultValues: {
            name: '',
        },
    });

    const inviteForm = useForm<z.infer<typeof inviteMemberSchema>>({
        resolver: zodResolver(inviteMemberSchema),
        defaultValues: {
            email: '',
            role: 'member',
        },
    });

    const updateForm = useForm<z.infer<typeof workspaceSchema>>({
        resolver: zodResolver(workspaceSchema),
        defaultValues: {
            name: '',
        },
    });

    const createWorkspace = useMutation({
        mutationFn: (values: z.infer<typeof workspaceSchema>) => workspacesApi.create({ name: values.name }),
        onSuccess: workspace => {
            capture(
                analyticsEvents.workspaceCreated,
                withWorkspaceProperties(
                    { workspaceId: workspace.id, name: workspace.name, role: 'owner' },
                    { member_count: workspace.members.length },
                ),
            );
            queryClient.invalidateQueries({ queryKey: ['workspaces'] });
            toast({
                title: 'Workspace created',
                description: 'The workspace has been successfully created.',
            });
            setIsAddWorkspaceModalOpen(false);
            form.reset();
        },
    });

    const inviteMember = useMutation({
        mutationFn: (values: z.infer<typeof inviteMemberSchema>) =>
            workspacesApi.addMember(currentWorkspace!.workspaceId, values.email, values.role),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['workspaces'] });
            toast({
                title: 'Member invited',
                description: 'The member has been successfully invited.',
            });
            setIsInviteMemberModalOpen(false);
            inviteForm.reset();
        },
    });

    const updateMemberRole = useMutation({
        mutationFn: ({ userId, role }: { userId: string; role: string }) =>
            workspacesApi.updateMemberRole(currentWorkspace!.workspaceId, userId, role),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['workspaces'] });
            toast({
                title: 'Role updated',
                description: 'The member role has been updated.',
            });
        },
    });

    const removeMember = useMutation({
        mutationFn: (userId: string) => workspacesApi.removeMember(currentWorkspace!.workspaceId, userId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['workspaces'] });
            toast({
                title: 'Member removed',
                description: 'The member has been removed from the workspace.',
            });
        },
    });

    const updateWorkspace = useMutation({
        mutationFn: (values: { id: string; name: string }) => workspacesApi.update(values.id, { name: values.name }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['workspaces'] });
            toast({
                title: 'Workspace updated',
                description: 'The workspace has been successfully updated.',
            });
            setIsEditWorkspaceModalOpen(false);
            updateForm.reset();
            setWorkspaceToEdit(null);
        },
    });

    const deleteWorkspace = useMutation({
        mutationFn: (id: string) => workspacesApi.delete(id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['workspaces'] });
            toast({
                title: 'Workspace deleted',
                description: 'The workspace has been successfully deleted.',
            });
            setIsDeleteWorkspaceModalOpen(false);
            setWorkspaceToEdit(null);
            setDeleteConfirmation('');

            // If current workspace was deleted, switch to another one or reload
            if (workspaceToEdit?.id === currentWorkspace?.workspaceId) {
                window.location.reload();
            }
        },
    });

    const onSubmit = (values: z.infer<typeof workspaceSchema>) => {
        createWorkspace.mutate(values);
    };

    const onInviteSubmit = (values: z.infer<typeof inviteMemberSchema>) => {
        inviteMember.mutate(values);
    };

    const onUpdateSubmit = (values: z.infer<typeof workspaceSchema>) => {
        if (workspaceToEdit) {
            updateWorkspace.mutate({ id: workspaceToEdit.id, name: values.name });
        }
    };

    const handleDeleteWorkspace = () => {
        if (workspaceToEdit && deleteConfirmation === workspaceToEdit.name) {
            deleteWorkspace.mutate(workspaceToEdit.id);
        }
    };

    const handleEditClick = (workspace: { id: string; name: string; members: MemberListItem[] }) => {
        const isOwner = workspace.members.find(m => m.userId === user?.userId)?.role === 'owner';
        setWorkspaceToEdit({ ...workspace, isOwner });
        updateForm.reset({ name: workspace.name });
        setDeleteConfirmation('');
        setIsEditWorkspaceModalOpen(true);
    };

    const getRoleBadgeVariant = (role: string) => {
        switch (role) {
            case 'owner':
                return 'default';
            case 'admin':
                return 'secondary';
            default:
                return 'outline';
        }
    };

    return (
        <>
            <Card>
                <CardHeader>
                    <CardTitle>My Workspaces</CardTitle>
                    <CardDescription>Manage your workspaces and create new ones</CardDescription>
                </CardHeader>
                <CardContent className='space-y-4'>
                    <div className='space-y-2'>
                        {workspaces.map(workspace => (
                            <div
                                key={workspace.id}
                                className='flex items-center justify-between p-3 border rounded-lg hover:bg-accent/50 transition-colors'
                            >
                                <div>
                                    <p className='font-medium'>{workspace.name}</p>
                                    <p className='text-sm text-muted-foreground'>
                                        {workspace.members.length} member(s)
                                    </p>
                                </div>
                                <div className='flex items-center gap-2'>
                                    {workspace.id === currentWorkspace?.workspaceId && <Badge>Current</Badge>}
                                    <Button variant='ghost' size='icon' onClick={() => handleEditClick(workspace)}>
                                        <Pencil className='h-4 w-4' />
                                    </Button>
                                </div>
                            </div>
                        ))}
                    </div>
                    <Button onClick={() => setIsAddWorkspaceModalOpen(true)}>
                        <Plus className='mr-2 h-4 w-4' />
                        Create Workspace
                    </Button>
                </CardContent>
            </Card>

            {currentWorkspace && (
                <Card>
                    <CardHeader>
                        <div className='flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between'>
                            <div className='min-w-0'>
                                <CardTitle>Workspace Members</CardTitle>
                                <CardDescription>Manage who has access to {currentWorkspace.name}</CardDescription>
                            </div>
                            <Button size='sm' className='shrink-0' onClick={() => setIsInviteMemberModalOpen(true)}>
                                <UserPlus className='mr-2 h-4 w-4' />
                                Invite Member
                            </Button>
                        </div>
                    </CardHeader>
                    <CardContent>
                        {isMobile ? (
                            <ul className='space-y-3'>
                                {members.map(member => {
                                    const isCurrentUser = member.userId === user?.userId;
                                    const isOwner = member.role === 'owner';
                                    return (
                                        <li key={member.userId} className='flex flex-col gap-2 rounded-lg border p-3'>
                                            <div className='flex min-w-0 items-start justify-between gap-2'>
                                                <div className='min-w-0'>
                                                    <p className='font-medium truncate'>{member.displayName}</p>
                                                    <p className='text-sm text-muted-foreground truncate'>
                                                        {member.email}
                                                    </p>
                                                </div>
                                                <div className='flex shrink-0 items-center gap-2'>
                                                    {isOwner || isCurrentUser ? (
                                                        <Badge variant={getRoleBadgeVariant(member.role)}>
                                                            {member.role}
                                                        </Badge>
                                                    ) : (
                                                        <>
                                                            <Select
                                                                defaultValue={member.role}
                                                                onValueChange={value =>
                                                                    updateMemberRole.mutate({
                                                                        userId: member.userId,
                                                                        role: value,
                                                                    })
                                                                }
                                                            >
                                                                <SelectTrigger className='h-8 w-[100px]'>
                                                                    <SelectValue />
                                                                </SelectTrigger>
                                                                <SelectContent>
                                                                    <SelectItem value='admin'>Admin</SelectItem>
                                                                    <SelectItem value='member'>Member</SelectItem>
                                                                </SelectContent>
                                                            </Select>
                                                            <Button
                                                                variant='ghost'
                                                                size='icon'
                                                                className='h-8 w-8'
                                                                onClick={() => removeMember.mutate(member.userId)}
                                                            >
                                                                <Trash2 className='h-4 w-4 text-destructive' />
                                                            </Button>
                                                        </>
                                                    )}
                                                </div>
                                            </div>
                                        </li>
                                    );
                                })}
                            </ul>
                        ) : (
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>User</TableHead>
                                        <TableHead>Email</TableHead>
                                        <TableHead>Role</TableHead>
                                        <TableHead className='w-[100px]'>Actions</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {members.map(member => {
                                        const isCurrentUser = member.userId === user?.userId;
                                        const isOwner = member.role === 'owner';

                                        return (
                                            <TableRow key={member.userId}>
                                                <TableCell className='font-medium'>{member.displayName}</TableCell>
                                                <TableCell>{member.email}</TableCell>
                                                <TableCell>
                                                    {isOwner || isCurrentUser ? (
                                                        <Badge variant={getRoleBadgeVariant(member.role)}>
                                                            {member.role}
                                                        </Badge>
                                                    ) : (
                                                        <Select
                                                            defaultValue={member.role}
                                                            onValueChange={value =>
                                                                updateMemberRole.mutate({
                                                                    userId: member.userId,
                                                                    role: value,
                                                                })
                                                            }
                                                        >
                                                            <SelectTrigger className='w-[120px]'>
                                                                <SelectValue />
                                                            </SelectTrigger>
                                                            <SelectContent>
                                                                <SelectItem value='admin'>Admin</SelectItem>
                                                                <SelectItem value='member'>Member</SelectItem>
                                                            </SelectContent>
                                                        </Select>
                                                    )}
                                                </TableCell>
                                                <TableCell>
                                                    {!isOwner && !isCurrentUser && (
                                                        <Button
                                                            variant='ghost'
                                                            size='icon'
                                                            onClick={() => removeMember.mutate(member.userId)}
                                                        >
                                                            <Trash2 className='h-4 w-4 text-destructive' />
                                                        </Button>
                                                    )}
                                                </TableCell>
                                            </TableRow>
                                        );
                                    })}
                                </TableBody>
                            </Table>
                        )}
                    </CardContent>
                </Card>
            )}

            <Dialog open={isAddWorkspaceModalOpen} onOpenChange={setIsAddWorkspaceModalOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Create Workspace</DialogTitle>
                        <DialogDescription>Create a new workspace to organize your finances.</DialogDescription>
                    </DialogHeader>
                    <Form {...form}>
                        <form onSubmit={form.handleSubmit(onSubmit)} className='space-y-4'>
                            <FormField
                                control={form.control}
                                name='name'
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Workspace Name</FormLabel>
                                        <FormControl>
                                            <Input placeholder='My Workspace' {...field} />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            <DialogFooter>
                                <Button type='submit' disabled={createWorkspace.isPending}>
                                    Create Workspace
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>

            <Dialog open={isEditWorkspaceModalOpen} onOpenChange={setIsEditWorkspaceModalOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Edit Workspace</DialogTitle>
                        <DialogDescription>Update or remove your workspace.</DialogDescription>
                    </DialogHeader>
                    <Form {...updateForm}>
                        <form onSubmit={updateForm.handleSubmit(onUpdateSubmit)} className='space-y-4'>
                            <FormField
                                control={updateForm.control}
                                name='name'
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Workspace Name</FormLabel>
                                        <FormControl>
                                            <Input placeholder='My Workspace' {...field} />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            <DialogFooter>
                                <Button type='submit' disabled={updateWorkspace.isPending}>
                                    Save
                                </Button>
                            </DialogFooter>
                            {workspaceToEdit?.isOwner && (
                                <div className='pt-6 border-t mt-6'>
                                    <div className='space-y-4'>
                                        <div>
                                            <h4 className='text-sm font-medium text-destructive'>Delete Workspace</h4>
                                            <p className='text-sm text-muted-foreground'>
                                                This action cannot be undone. This will permanently delete the workspace
                                                and all associated data.
                                            </p>
                                        </div>
                                        <Button
                                            type='button'
                                            variant='outline'
                                            className='w-full border-destructive text-destructive hover:bg-destructive hover:text-destructive-foreground'
                                            onClick={() => {
                                                setIsEditWorkspaceModalOpen(false);
                                                setIsDeleteWorkspaceModalOpen(true);
                                            }}
                                        >
                                            Delete Workspace
                                        </Button>
                                    </div>
                                </div>
                            )}
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>

            <Dialog open={isDeleteWorkspaceModalOpen} onOpenChange={setIsDeleteWorkspaceModalOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Delete Workspace</DialogTitle>
                        <DialogDescription>
                            This action cannot be undone. This will permanently delete the workspace{' '}
                            <span className='font-bold'>{workspaceToEdit?.name}</span> and all associated data.
                        </DialogDescription>
                    </DialogHeader>
                    <div className='space-y-4'>
                        <div className='space-y-2'>
                            <Label>Type "{workspaceToEdit?.name}" to confirm</Label>
                            <Input
                                value={deleteConfirmation}
                                onChange={e => setDeleteConfirmation(e.target.value)}
                                placeholder={workspaceToEdit?.name}
                            />
                        </div>
                        <DialogFooter>
                            <Button
                                variant='outline'
                                onClick={() => {
                                    setIsDeleteWorkspaceModalOpen(false);
                                    setIsEditWorkspaceModalOpen(true);
                                }}
                                disabled={deleteWorkspace.isPending}
                            >
                                Cancel
                            </Button>
                            <Button
                                variant='destructive'
                                disabled={deleteConfirmation !== workspaceToEdit?.name || deleteWorkspace.isPending}
                                onClick={handleDeleteWorkspace}
                            >
                                {deleteWorkspace.isPending ? 'Deleting...' : 'Delete Workspace'}
                            </Button>
                        </DialogFooter>
                    </div>
                </DialogContent>
            </Dialog>

            <Dialog open={isInviteMemberModalOpen} onOpenChange={setIsInviteMemberModalOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Invite Member</DialogTitle>
                        <DialogDescription>Invite a new member to this workspace.</DialogDescription>
                    </DialogHeader>
                    <Form {...inviteForm}>
                        <form onSubmit={inviteForm.handleSubmit(onInviteSubmit)} className='space-y-4'>
                            <FormField
                                control={inviteForm.control}
                                name='email'
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Email</FormLabel>
                                        <FormControl>
                                            <Input type='email' placeholder='user@example.com' {...field} />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            <FormField
                                control={inviteForm.control}
                                name='role'
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Role</FormLabel>
                                        <Select onValueChange={field.onChange} value={field.value}>
                                            <FormControl>
                                                <SelectTrigger>
                                                    <SelectValue placeholder='Select role' />
                                                </SelectTrigger>
                                            </FormControl>
                                            <SelectContent>
                                                <SelectItem value='admin'>Admin</SelectItem>
                                                <SelectItem value='member'>Member</SelectItem>
                                            </SelectContent>
                                        </Select>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            <DialogFooter>
                                <Button type='submit' disabled={inviteMember.isPending}>
                                    Invite Member
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>
        </>
    );
}
