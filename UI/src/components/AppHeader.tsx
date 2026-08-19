import { SidebarTrigger, useSidebar } from '@/components/ui/sidebar';
import { Button } from '@/components/ui/button';
import { LogOut } from 'lucide-react';
import { useLogout } from '@/hooks/use-logout';
import { WorkspaceSwitcher } from '@/components/WorkspaceSwitcher';

export function AppHeader() {
    const { isMobile } = useSidebar();
    const logout = useLogout();

    return (
        <header className='flex h-14 min-w-0 items-center justify-between gap-2 border-b border-border bg-card px-3 sm:px-4'>
            <div className='flex min-w-0 flex-1 items-center gap-2 sm:gap-4'>
                <SidebarTrigger className='shrink-0' />
                <WorkspaceSwitcher />
            </div>

            {/* On mobile the sidebar is off-canvas, so logout also lives in the header there. */}
            {isMobile && (
                <Button variant='ghost' size='sm' className='shrink-0' onClick={logout}>
                    <LogOut className='mr-2 h-4 w-4' />
                    Log out
                </Button>
            )}
        </header>
    );
}
