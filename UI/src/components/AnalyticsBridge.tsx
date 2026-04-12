import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import {
    resetAnalyticsUser,
    syncAnalyticsUser,
    syncAnalyticsWorkspace,
    trackScreenView,
    trackWorkspaceChange,
    useAnalytics,
} from '@/lib/analytics';

export function AnalyticsBridge() {
    const location = useLocation();
    const { user, isLoading } = useAuth();
    const { currentWorkspace } = useWorkspace();
    const { posthog } = useAnalytics();
    const pathSegments = location.pathname.split('/').filter(Boolean);
    const routeWorkspaceId = pathSegments[0] === 'workspaces' ? pathSegments[1] : null;
    const analyticsWorkspace =
        routeWorkspaceId && currentWorkspace?.workspaceId !== routeWorkspaceId ? null : currentWorkspace;

    useEffect(() => {
        if (!posthog || isLoading) return;

        if (user) {
            syncAnalyticsUser(posthog, user);
            return;
        }

        resetAnalyticsUser(posthog);
    }, [isLoading, posthog, user]);

    useEffect(() => {
        if (!posthog) return;

        syncAnalyticsWorkspace(posthog, analyticsWorkspace);
        trackWorkspaceChange(posthog, analyticsWorkspace);
    }, [analyticsWorkspace, posthog]);

    useEffect(() => {
        if (!posthog) return;

        trackScreenView(posthog, location.pathname, location.search, analyticsWorkspace, routeWorkspaceId);
    }, [analyticsWorkspace, location.pathname, location.search, posthog, routeWorkspaceId]);

    return null;
}
