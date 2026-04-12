import { createContext, useContext, useState, ReactNode } from 'react';
import { WorkspaceListItem } from '@/models/workspace';
import { useAuth } from './AuthContext';

interface WorkspaceContextType {
    workspaces: WorkspaceListItem[];
    currentWorkspace?: WorkspaceListItem;
    setCurrentWorkspaceId: (workspaceId: string | null) => void;
    isLoading: boolean;
}

const WorkspaceContext = createContext<WorkspaceContextType | undefined>(undefined);

export function WorkspaceProvider({ children }: { children: ReactNode }) {
    const { user, isLoading } = useAuth();
    const [currentWorkspaceId, setCurrentWorkspaceIdState] = useState<string | null>(null);

    // Wrapper that clears filters when workspace changes
    const setCurrentWorkspaceId = (newWorkspaceId: string | null) => {
        if (newWorkspaceId !== currentWorkspaceId && currentWorkspaceId !== null) {
            // Clear filter localStorage keys when switching workspaces
            // These keys are used by useQueryString hook for filter persistence
            const filterKeys = [''];
            filterKeys.forEach(key => localStorage.removeItem(key));
        }

        if (user && !user.workspaces.some(workspace => workspace.workspaceId === newWorkspaceId)) {
            throw new Error('Workspace not found');
        }

        setCurrentWorkspaceIdState(newWorkspaceId);
    };

    return (
        <WorkspaceContext.Provider
            value={{
                workspaces: user?.workspaces ?? [],
                currentWorkspace:
                    user?.workspaces?.find(w => w.workspaceId === currentWorkspaceId) ?? user?.workspaces?.[0],
                setCurrentWorkspaceId,
                isLoading: isLoading,
            }}
        >
            {children}
        </WorkspaceContext.Provider>
    );
}

export function useWorkspace() {
    const context = useContext(WorkspaceContext);
    if (context === undefined) {
        throw new Error('useWorkspace must be used within a WorkspaceProvider');
    }
    return context;
}
