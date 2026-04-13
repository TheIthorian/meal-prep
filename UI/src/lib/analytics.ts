import { usePostHog } from '@posthog/react';
import type { UserResponse } from '@/models/user';
import type { WorkspaceListItem } from '@/models/workspace';

type AnalyticsValue = boolean | number | string | null | undefined;
type AnalyticsProperties = Record<string, AnalyticsValue>;
type AnalyticsClient = ReturnType<typeof usePostHog>;

export const analyticsEvents = {
    screenViewed: 'screen_viewed',
    userLoggedIn: 'user_logged_in',
    userLoggedOut: 'user_logged_out',
    userRegistered: 'user_registered',
    workspaceCreated: 'workspace_created',
    workspaceSwitched: 'workspace_switched',
    recipeDeleted: 'recipe_deleted',
    shoppingListDeleted: 'shopping_list_deleted',
    mcpAccessTokenCreated: 'mcp_access_token_created',
    mcpAccessTokenRevoked: 'mcp_access_token_revoked',
} as const;

type AnalyticsEventName = (typeof analyticsEvents)[keyof typeof analyticsEvents];

let lastIdentifiedUserSignature: string | null = null;
let lastTrackedScreenKey: string | null = null;
let lastWorkspaceGroupSignature: string | null = null;
let lastWorkspaceId: string | null = null;

const sanitizeProperties = (properties?: AnalyticsProperties) =>
    Object.fromEntries(Object.entries(properties ?? {}).filter(([, value]) => value !== undefined));

const getScreenName = (pathname: string) => {
    const segments = pathname.split('/').filter(Boolean);

    if (segments.length === 0) return 'home';
    if (segments[0] === 'workspaces') return segments[2] ?? 'workspace';
    return segments[0];
};

export const withWorkspaceProperties = (
    workspace?: WorkspaceListItem | null,
    properties: AnalyticsProperties = {},
): AnalyticsProperties =>
    sanitizeProperties({
        workspace_id: workspace?.workspaceId,
        workspace_name: workspace?.name,
        workspace_role: workspace?.role,
        ...properties,
    });

export function useAnalytics() {
    const posthog = usePostHog();

    const capture = (event: AnalyticsEventName, properties?: AnalyticsProperties) => {
        posthog?.capture(event, sanitizeProperties(properties));
    };

    return { capture, posthog };
}

export const syncAnalyticsUser = (posthog: AnalyticsClient, user: UserResponse) => {
    if (!posthog) return;

    const signature = `${user.userId}:${user.email}:${user.displayName}:${user.workspaces.length}`;
    if (signature === lastIdentifiedUserSignature) return;

    posthog.identify(
        user.userId,
        sanitizeProperties({
            email: user.email,
            display_name: user.displayName,
            workspace_count: user.workspaces.length,
        }),
    );

    lastIdentifiedUserSignature = signature;
};

export const resetAnalyticsUser = (posthog: AnalyticsClient) => {
    if (!posthog) return;

    posthog.reset();
    lastIdentifiedUserSignature = null;
    lastTrackedScreenKey = null;
    lastWorkspaceGroupSignature = null;
    lastWorkspaceId = null;
};

export const syncAnalyticsWorkspace = (posthog: AnalyticsClient, workspace?: WorkspaceListItem | null) => {
    if (!posthog || !workspace) return;

    const signature = `${workspace.workspaceId}:${workspace.name}:${workspace.role}`;
    if (signature === lastWorkspaceGroupSignature) return;

    posthog.group(
        'workspace',
        workspace.workspaceId,
        sanitizeProperties({
            name: workspace.name,
            role: workspace.role,
        }),
    );

    lastWorkspaceGroupSignature = signature;
};

export const trackWorkspaceChange = (posthog: AnalyticsClient, workspace?: WorkspaceListItem | null) => {
    if (!posthog) return;

    if (!workspace) {
        lastWorkspaceId = null;
        return;
    }

    if (lastWorkspaceId === null) {
        lastWorkspaceId = workspace.workspaceId;
        return;
    }

    if (lastWorkspaceId === workspace.workspaceId) return;

    posthog.capture(
        analyticsEvents.workspaceSwitched,
        withWorkspaceProperties(workspace, { previous_workspace_id: lastWorkspaceId }),
    );

    lastWorkspaceId = workspace.workspaceId;
};

export const trackScreenView = (
    posthog: AnalyticsClient,
    pathname: string,
    search: string,
    workspace?: WorkspaceListItem | null,
    workspaceId?: string | null,
) => {
    if (!posthog) return;

    const screenKey = `${pathname}${search}`;
    if (screenKey === lastTrackedScreenKey) return;

    posthog.capture(
        analyticsEvents.screenViewed,
        withWorkspaceProperties(workspace, {
            path: pathname,
            screen_name: getScreenName(pathname),
            search: search || undefined,
            workspace_id: workspace?.workspaceId ?? workspaceId ?? undefined,
        }),
    );

    lastTrackedScreenKey = screenKey;
};
