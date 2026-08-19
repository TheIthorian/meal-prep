import { NavLink } from 'react-router-dom';
import { MealPrepUserMenu } from '@/components/meal-prep/MealPrepUserMenu';
import { mealPrepNavItems } from '@/lib/meal-prep-nav';

interface MealPrepBottomNavProps {
    workspaceId: string;
}

/**
 * The mobile tab bar. It lives in its own component because pages outside the workspace layout —
 * the public share pages — still show it once the visitor is signed in, so that following a share
 * link does not strand them without a way back into the app.
 */
export function MealPrepBottomNav({ workspaceId }: MealPrepBottomNavProps) {
    return (
        <nav className='safe-area-pb fixed inset-x-0 bottom-0 z-30 border-t border-border bg-card/95 backdrop-blur-md md:hidden'>
            <div className='flex items-center justify-around overflow-x-auto py-2'>
                {mealPrepNavItems(workspaceId).map(item => (
                    <NavLink
                        key={item.to}
                        to={item.to}
                        end={item.end}
                        className={({ isActive }) =>
                            `flex min-w-[3.25rem] shrink-0 flex-col items-center gap-0.5 rounded-lg px-2 py-1.5 transition-colors ${
                                isActive ? 'text-primary' : 'text-muted-foreground'
                            }`
                        }
                    >
                        <item.icon className='h-5 w-5' />
                        <span className='max-w-[4.5rem] truncate text-center text-[10px] font-medium'>
                            {item.label}
                        </span>
                    </NavLink>
                ))}

                {/* Settings and logout sit behind the avatar here too, opening upwards. */}
                <div className='flex min-w-[3.25rem] shrink-0 items-center justify-center px-2 py-1.5'>
                    <MealPrepUserMenu workspaceId={workspaceId} align='end' side='top' />
                </div>
            </div>
        </nav>
    );
}
