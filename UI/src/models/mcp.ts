export interface McpAccessTokenCreated {
    id: string;
    workspaceId: string;
    token: string;
    mcpUrl: string;
}

export interface McpAccessTokenListItem {
    id: string;
    workspaceId: string;
    name: string | null;
    createdAt: string;
    lastUsedAt: string | null;
    revokedAt: string | null;
}
