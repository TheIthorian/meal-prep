import { useEffect, useState } from 'react';
import { NavLink, Outlet, useParams } from 'react-router-dom';
import { BookOpen, Calendar, ChefHat, ShoppingCart, Settings } from 'lucide-react';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import NotFoundError from '@/pages/NotFoundError';
import { WorkspaceSwitcher } from '@/components/WorkspaceSwitcher';

function workspacePath(workspaceId: string, subPath: string) {
    const trimmed = subPath.replace(/^\//, '');
    return `/workspaces/${workspaceId}/${trimmed}`;
}

export function MealPrepAppLayout() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const { setCurrentWorkspaceId } = useWorkspace();
    const [workspaceInvalid, setWorkspaceInvalid] = useState(false);

    useEffect(() => {
        if (!workspaceId) return;
        try {
            setCurrentWorkspaceId(workspaceId);
        } catch {
            setWorkspaceInvalid(true);
        }
    }, [setCurrentWorkspaceId, workspaceId]);

    const navItems = [
        { to: workspacePath(workspaceId, '/'), icon: BookOpen, label: 'Recipes', end: true },
        { to: workspacePath(workspaceId, 'planner'), icon: Calendar, label: 'Planner', end: false },
        { to: workspacePath(workspaceId, 'shopping'), icon: ShoppingCart, label: 'Shopping', end: false },
        { to: workspacePath(workspaceId, 'settings'), icon: Settings, label: 'Settings', end: false },
    ];

    if (workspaceInvalid) {
        return <NotFoundError />;
    }

    return (
        <div className='flex min-h-screen flex-col bg-background'>
            <header className='sticky top-0 z-30 hidden min-w-0 items-center justify-between gap-4 border-b border-border bg-card/80 px-4 py-4 backdrop-blur-sm md:flex lg:px-8'>
                <div className='flex min-w-0 flex-1 items-center gap-3 lg:gap-4'>
                    <div className='flex min-w-0 shrink-0 items-center gap-2'>
                        <ChefHat className='h-7 w-7 shrink-0 text-primary' aria-hidden />
                        <h1 className='font-heading truncate text-xl tracking-tight text-foreground'>Meal Prep</h1>
                    </div>
                    <WorkspaceSwitcher />
                </div>
                <nav className='flex shrink-0 items-center gap-1'>
                    {navItems.map(item => (
                        <NavLink
                            key={item.to}
                            to={item.to}
                            end={item.end}
                            className={({ isActive }) =>
                                `flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium transition-colors ${
                                    isActive
                                        ? 'bg-primary/10 text-primary'
                                        : 'text-muted-foreground hover:bg-secondary hover:text-foreground'
                                }`
                            }
                        >
                            <item.icon className='h-4 w-4' />
                            {item.label}
                        </NavLink>
                    ))}
                </nav>
            </header>

            <main className='flex-1 pb-20 md:pb-0'>
                <Outlet />
            </main>

            <nav className='safe-area-pb fixed inset-x-0 bottom-0 z-30 border-t border-border bg-card/95 backdrop-blur-md md:hidden'>
                <div className='flex items-center justify-around py-2'>
                    {navItems.map(item => (
                        <NavLink
                            key={item.to}
                            to={item.to}
                            end={item.end}
                            className={({ isActive }) =>
                                `flex flex-col items-center gap-0.5 rounded-lg px-3 py-1.5 transition-colors ${
                                    isActive ? 'text-primary' : 'text-muted-foreground'
                                }`
                            }
                        >
                            <item.icon className='h-5 w-5' />
                            <span className='text-[10px] font-medium'>{item.label}</span>
                        </NavLink>
                    ))}
                </div>
            </nav>
        </div>
    );
}
