import { LogOut, Settings } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useAuth } from '@/contexts/AuthContext';
import { useLogout } from '@/hooks/use-logout';

interface MealPrepUserMenuProps {
    workspaceId: string;
    align?: 'start' | 'center' | 'end';
    side?: 'top' | 'bottom';
}

/** Two letters at most, so the avatar stays a circle rather than a pill. */
function initialsFor(name: string) {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

/** The account menu behind the avatar: profile settings and signing out. */
export function MealPrepUserMenu({ workspaceId, align = 'end', side = 'bottom' }: MealPrepUserMenuProps) {
    const { user } = useAuth();
    const logout = useLogout();
    const navigate = useNavigate();

    const name = user?.displayName || user?.email || '';
    const settingsPath = workspaceId ? `/workspaces/${workspaceId}/settings` : '/settings';

    return (
        <DropdownMenu>
            <DropdownMenuTrigger
                aria-label='Open account menu'
                className='shrink-0 rounded-full outline-none ring-offset-background transition-opacity hover:opacity-80 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
            >
                <Avatar className='h-8 w-8'>
                    <AvatarFallback className='bg-primary/10 text-xs font-semibold text-primary'>
                        {initialsFor(name)}
                    </AvatarFallback>
                </Avatar>
            </DropdownMenuTrigger>
            <DropdownMenuContent align={align} side={side} className='w-56'>
                {name && (
                    <>
                        <DropdownMenuLabel className='truncate font-normal'>
                            <span className='block text-xs text-muted-foreground'>Signed in as</span>
                            <span className='block truncate font-medium'>{name}</span>
                        </DropdownMenuLabel>
                        <DropdownMenuSeparator />
                    </>
                )}
                <DropdownMenuItem onSelect={() => navigate(settingsPath)}>
                    <Settings className='mr-2 h-4 w-4' aria-hidden />
                    Settings
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={() => void logout()}>
                    <LogOut className='mr-2 h-4 w-4' aria-hidden />
                    Log out
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    );
}
