import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';

import { useAuth } from '@/contexts/AuthContext';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { toast } from '@/hooks/use-toast';

/**
 * Single logout entry point: captures the analytics event, clears the session
 * and redirects to the login page.
 */
export function useLogout() {
    const { logout } = useAuth();
    const { currentWorkspace } = useWorkspace();
    const navigate = useNavigate();
    const { capture } = useAnalytics();

    return useCallback(async () => {
        capture(analyticsEvents.userLoggedOut, withWorkspaceProperties(currentWorkspace));

        try {
            await logout();
            navigate('/login');
        } catch {
            toast({
                variant: 'destructive',
                title: 'Unable to log out',
                description: 'Please try again.',
            });
        }
    }, [capture, currentWorkspace, logout, navigate]);
}
