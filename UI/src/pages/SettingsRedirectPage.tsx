import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { LoadingState } from '@/components/common/LoadingState';
import Settings from '@/pages/Settings';

/**
 * Sends users with a workspace to the workspace-scoped settings route; otherwise shows account settings.
 */
export default function SettingsRedirectPage() {
    const { currentWorkspace, isLoading } = useWorkspace();
    const navigate = useNavigate();

    useEffect(() => {
        if (isLoading) return;
        if (currentWorkspace) {
            navigate(`/workspaces/${currentWorkspace.workspaceId}/settings`, { replace: true });
        }
    }, [currentWorkspace, isLoading, navigate]);

    if (isLoading) {
        return (
            <div className='flex min-h-[40vh] items-center justify-center'>
                <LoadingState label='Loading…' />
            </div>
        );
    }

    if (!currentWorkspace) {
        return <Settings />;
    }

    return (
        <div className='flex min-h-[40vh] items-center justify-center'>
            <LoadingState label='Loading…' />
        </div>
    );
}
