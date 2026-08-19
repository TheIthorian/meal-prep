import { ChefHat, CircleHelp, LogOut, Settings } from 'lucide-react';
import { NavLink } from '@/components/NavLink';
import {
    Sidebar,
    SidebarContent,
    SidebarFooter,
    SidebarGroup,
    SidebarGroupContent,
    SidebarGroupLabel,
    SidebarMenu,
    SidebarMenuButton,
    SidebarMenuItem,
    useSidebar,
} from '@/components/ui/sidebar';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { useAuth } from '@/contexts/AuthContext';
import { useLogout } from '@/hooks/use-logout';

const menuItems = [{ title: 'Settings', url: 'settings', icon: Settings }];

export function AppSidebar() {
    const { state, isMobile, setOpenMobile } = useSidebar();
    const { currentWorkspace } = useWorkspace();
    const { user } = useAuth();
    const logout = useLogout();
    const collapsed = state === 'collapsed';

    const handleNavClick = () => {
        // Close sidebar on mobile after navigation
        if (isMobile) {
            setOpenMobile(false);
        }
    };

    // If no workspace, show minimal menu
    const effectiveMenuItems = currentWorkspace ? menuItems : menuItems.filter(item => item.url === 'settings');

    return (
        <Sidebar className={collapsed ? 'w-14' : 'w-[--sidebar-width]'} collapsible='icon'>
            <SidebarContent>
                <div className={`flex h-14 items-center px-4 ${collapsed ? 'justify-center' : 'gap-2'}`}>
                    <ChefHat className='h-7 w-7 shrink-0 text-sidebar-foreground' aria-hidden />
                    {!collapsed && <h1 className='text-xl font-bold text-sidebar-foreground'>Meal Prep</h1>}
                </div>

                <SidebarGroup>
                    <SidebarGroupLabel>Main</SidebarGroupLabel>
                    <SidebarGroupContent>
                        <SidebarMenu>
                            {effectiveMenuItems.map(item => (
                                <SidebarMenuItem key={item.title}>
                                    <SidebarMenuButton asChild>
                                        <NavLink
                                            to={
                                                currentWorkspace
                                                    ? `/workspaces/${currentWorkspace.workspaceId}/${item.url}`
                                                    : `/${item.url}`
                                            }
                                            end
                                            className='hover:bg-sidebar-accent'
                                            activeClassName='bg-sidebar-accent text-sidebar-primary font-medium'
                                            onClick={handleNavClick}
                                        >
                                            <item.icon className='h-4 w-4' />
                                            {!collapsed && <span>{item.title}</span>}
                                        </NavLink>
                                    </SidebarMenuButton>
                                </SidebarMenuItem>
                            ))}
                        </SidebarMenu>
                    </SidebarGroupContent>
                </SidebarGroup>
            </SidebarContent>
            <SidebarFooter>
                <SidebarMenu>
                    <SidebarMenuItem>
                        <SidebarMenuButton asChild tooltip='Help'>
                            <NavLink
                                to='/help'
                                className='hover:bg-sidebar-accent'
                                activeClassName='bg-sidebar-accent text-sidebar-primary font-medium'
                                onClick={handleNavClick}
                            >
                                <CircleHelp className='h-4 w-4' />
                                {!collapsed && <span>Help</span>}
                            </NavLink>
                        </SidebarMenuButton>
                    </SidebarMenuItem>
                    <SidebarMenuItem>
                        <SidebarMenuButton
                            onClick={logout}
                            tooltip='Log out'
                            aria-label='Log out'
                            className='hover:bg-sidebar-accent'
                        >
                            <LogOut className='h-4 w-4' />
                            {!collapsed && <span>Log out</span>}
                        </SidebarMenuButton>
                    </SidebarMenuItem>
                </SidebarMenu>
                {!collapsed && (user?.displayName || user?.email) && (
                    <p className='truncate px-2 pb-1 text-xs text-muted-foreground'>
                        {user?.displayName || user?.email}
                    </p>
                )}
            </SidebarFooter>
        </Sidebar>
    );
}
