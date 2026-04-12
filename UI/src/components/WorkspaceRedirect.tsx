import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useWorkspace } from '@/contexts/WorkspaceContext';

export function WorkspaceRedirect() {
    const { currentWorkspace, workspaces, isLoading } = useWorkspace();
    const navigate = useNavigate();

    useEffect(() => {
        if (currentWorkspace) {
            navigate(`/workspaces/${currentWorkspace.workspaceId}/`, { replace: true });
        } else if (workspaces.length > 0) {
            navigate(`/workspaces/${workspaces[0].workspaceId}/`, { replace: true });
        } else if (!isLoading) {
            navigate('/settings', { replace: true });
        }
    }, [currentWorkspace, workspaces, navigate, isLoading]);

    return null;
}
