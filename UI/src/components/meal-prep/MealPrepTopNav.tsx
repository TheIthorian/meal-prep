import { NavLink } from 'react-router-dom';
import { ChefHat } from 'lucide-react';
import { WorkspaceSwitcher } from '@/components/WorkspaceSwitcher';
import { mealPrepNavItems } from '@/lib/meal-prep-nav';

interface MealPrepTopNavProps {
    workspaceId: string;
}

/**
 * The desktop header. Like the mobile tab bar it is a standalone component because the public
 * share pages render outside the workspace layout but still show it to a signed-in visitor.
 */
export function MealPrepTopNav({ workspaceId }: MealPrepTopNavProps) {
    return (
        <header className='sticky top-0 z-30 hidden min-w-0 items-center justify-between gap-4 border-b border-border bg-card/80 px-4 py-4 backdrop-blur-sm md:flex lg:px-8'>
            <div className='flex min-w-0 flex-1 items-center gap-3 lg:gap-4'>
                <div className='flex min-w-0 shrink items-center gap-2'>
                    <ChefHat className='h-7 w-7 shrink-0 text-primary' aria-hidden />
                    <h1 className='font-heading truncate text-xl tracking-tight text-foreground'>Meal Prep</h1>
                </div>
                <WorkspaceSwitcher />
            </div>
            <nav className='flex min-w-0 shrink items-center gap-1'>
                {mealPrepNavItems(workspaceId).map(item => (
                    <NavLink
                        key={item.to}
                        to={item.to}
                        end={item.end}
                        className={({ isActive }) =>
                            `flex shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors xl:px-4 ${
                                isActive
                                    ? 'bg-primary/10 text-primary'
                                    : 'text-muted-foreground hover:bg-secondary hover:text-foreground'
                            }`
                        }
                    >
                        <item.icon className='h-4 w-4 shrink-0' aria-hidden />
                        <span className='hidden lg:inline'>{item.label}</span>
                        <span className='sr-only lg:hidden'>{item.label}</span>
                    </NavLink>
                ))}
            </nav>
        </header>
    );
}
