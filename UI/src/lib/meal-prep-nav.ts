import { BookOpen, FolderOpen, ListOrdered, ShoppingCart } from 'lucide-react';

function workspacePath(workspaceId: string, subPath: string) {
    const trimmed = subPath.replace(/^\//, '');
    return `/workspaces/${workspaceId}/${trimmed}`;
}

/**
 * The workspace destinations shown in both the desktop header and the mobile tab bar.
 * Settings is not one of them: it lives in the account menu behind the avatar.
 */
export function mealPrepNavItems(workspaceId: string) {
    return [
        { to: workspacePath(workspaceId, '/'), icon: BookOpen, label: 'Recipes', end: true },
        { to: workspacePath(workspaceId, 'collections'), icon: FolderOpen, label: 'Collections', end: false },
        { to: workspacePath(workspaceId, 'next-meals'), icon: ListOrdered, label: 'Next Meals', end: false },
        { to: workspacePath(workspaceId, 'shopping'), icon: ShoppingCart, label: 'Shopping', end: false },
    ];
}
