import { Outlet, useParams } from 'react-router-dom';
import { SidebarProvider } from '@/components/ui/sidebar';
import { AppSidebar } from '@/components/AppSidebar';
import { AppHeader } from '@/components/AppHeader';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { useEffect, useState } from 'react';
import NotFoundError from '@/pages/NotFoundError';

export function AppLayout() {
    const { workspaceId } = useParams<{ workspaceId: string }>();
    const { setCurrentWorkspaceId } = useWorkspace();
    const [isWorkspaceNotFound, setIsWorkspaceNotFound] = useState(false);

    useEffect(() => {
        if (workspaceId) {
            try {
                setCurrentWorkspaceId(workspaceId);
            } catch (error) {
                setIsWorkspaceNotFound(true);
            }
        }
    }, [workspaceId, setCurrentWorkspaceId]);

    if (isWorkspaceNotFound) {
        return <NotFoundError />;
    }

    return (
        <SidebarProvider>
            <div className='flex min-h-screen w-full min-w-0'>
                <AppSidebar />
                <div className='flex min-w-0 flex-1 flex-col'>
                    <AppHeader />
                    <main className='min-w-0 flex-1 overflow-x-hidden p-4 md:p-6 lg:p-8'>
                        <Outlet />
                    </main>
                </div>
            </div>
        </SidebarProvider>
    );
}
