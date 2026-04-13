import { useNavigate } from 'react-router-dom';
import { ChevronDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { cn } from '@/lib/utils';

interface WorkspaceSwitcherProps {
    className?: string;
    triggerClassName?: string;
}

/** Dropdown to change the active workspace and navigate to that workspace’s home route. */
export function WorkspaceSwitcher({ className, triggerClassName }: WorkspaceSwitcherProps) {
    const { currentWorkspace, workspaces, setCurrentWorkspaceId } = useWorkspace();
    const navigate = useNavigate();

    if (workspaces.length === 0) return null;

    return (
        <div className={cn('min-w-0 shrink-0', className)}>
            <DropdownMenu>
                <DropdownMenuTrigger asChild>
                    <Button
                        variant='outline'
                        className={cn(
                            'min-w-0 max-w-[min(200px,42vw)] gap-1 sm:max-w-[min(240px,28vw)] sm:gap-2',
                            triggerClassName,
                        )}
                    >
                        <span className='truncate'>{currentWorkspace?.name || 'Select workspace'}</span>
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
        </div>
    );
}
