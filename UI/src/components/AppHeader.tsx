import { SidebarTrigger } from '@/components/ui/sidebar';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/contexts/AuthContext';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { User, LogOut, ChevronDown } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';

export function AppHeader() {
    const { user, logout } = useAuth();
    const { currentWorkspace, workspaces, setCurrentWorkspaceId } = useWorkspace();
    const navigate = useNavigate();
    const { capture } = useAnalytics();

    const handleLogout = async () => {
        capture(analyticsEvents.userLoggedOut, withWorkspaceProperties(currentWorkspace));
        await logout();
        navigate('/login');
    };

    return (
        <header className='flex h-14 min-w-0 items-center justify-between gap-2 border-b border-border bg-card px-3 sm:px-4'>
            <div className='flex min-w-0 flex-1 items-center gap-2 sm:gap-4'>
                <SidebarTrigger className='shrink-0' />

                {workspaces.length > 0 && (
                    <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                            <Button
                                variant='outline'
                                className='min-w-0 max-w-[min(180px,50vw)] gap-1 sm:max-w-none sm:gap-2'
                            >
                                <span className='truncate'>{currentWorkspace?.name || 'Select Workspace'}</span>
                                <ChevronDown className='h-4 w-4 shrink-0' />
                            </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align='start'>
                            <DropdownMenuLabel>Workspaces</DropdownMenuLabel>
                            <DropdownMenuSeparator />
                            {workspaces.map(workspace => (
                                <DropdownMenuItem
                                    key={workspace.workspaceId}
                                    onClick={() => {
                                        setCurrentWorkspaceId(workspace.workspaceId);
                                        navigate(`/workspaces/${workspace.workspaceId}/`);
                                    }}
                                >
                                    {workspace.name}
                                </DropdownMenuItem>
                            ))}
                        </DropdownMenuContent>
                    </DropdownMenu>
                )}
            </div>

            <DropdownMenu>
                <DropdownMenuTrigger asChild>
                    <Button variant='ghost' size='icon' className='shrink-0'>
                        <User className='h-5 w-5' />
                    </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align='end'>
                    <DropdownMenuLabel>{user?.displayName || user?.email}</DropdownMenuLabel>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                        onClick={() =>
                            navigate(
                                currentWorkspace ? `/workspaces/${currentWorkspace.workspaceId}/settings` : '/settings',
                            )
                        }
                    >
                        <User className='mr-2 h-4 w-4' />
                        Settings
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={handleLogout}>
                        <LogOut className='mr-2 h-4 w-4' />
                        Logout
                    </DropdownMenuItem>
                </DropdownMenuContent>
            </DropdownMenu>
        </header>
    );
}
