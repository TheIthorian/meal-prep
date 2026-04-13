import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Building2, Cable, SlidersHorizontal, UserRound } from 'lucide-react';
import { useQueryString } from '@/hooks/use-query-string';
import { ProfileSettings } from '@/components/settings/ProfileSettings';
import { PreferencesSettings } from '@/components/settings/PreferencesSettings';
import { WorkspaceSettings } from '@/components/settings/WorkspaceSettings';
import { IntegrationsSettings } from '@/components/settings/IntegrationsSettings';
import { cn } from '@/lib/utils';

const tabs = [
    { value: 'profile', label: 'Profile', icon: UserRound },
    { value: 'preferences', label: 'Preferences', icon: SlidersHorizontal },
    { value: 'integrations', label: 'Integrations', icon: Cable },
    { value: 'workspaces', label: 'Workspaces', icon: Building2 },
] as const;

export default function Settings() {
    const { params, setParam } = useQueryString(['tab']);
    const [activeTab, setActiveTab] = useState(params.tab || 'profile');

    useEffect(() => {
        if (params.tab) {
            setActiveTab(params.tab);
        }
    }, [params.tab]);

    function handleTabChange(value: string) {
        setActiveTab(value);
        setParam('tab', value);
    }

    return (
        <div className='mx-auto max-w-5xl px-4 py-6 md:px-8 md:py-10'>
            <motion.div
                initial={{ opacity: 0, y: -8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3 }}
                className='mb-8 md:mb-10'
            >
                <h1 className='font-heading text-3xl text-foreground md:text-4xl'>Settings</h1>
                <p className='mt-1.5 max-w-xl text-muted-foreground'>
                    Your account, how Meal Prep looks, integrations, and workspace access.
                </p>
            </motion.div>

            <Tabs
                value={activeTab}
                onValueChange={handleTabChange}
                orientation='vertical'
                className='flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10'
            >
                <TabsList
                    aria-label='Settings sections'
                    className={cn(
                        'flex h-auto w-full flex-shrink-0 flex-row flex-wrap gap-1 rounded-xl border border-border/80 bg-muted/40 p-1.5',
                        'lg:sticky lg:top-24 lg:max-w-[13.5rem] lg:flex-col lg:flex-nowrap lg:items-stretch lg:justify-start',
                    )}
                >
                    {tabs.map(({ value, label, icon: Icon }) => (
                        <TabsTrigger
                            key={value}
                            value={value}
                            className={cn(
                                'flex flex-1 items-center justify-center gap-2 rounded-lg px-3 py-2.5 text-sm font-medium sm:flex-none',
                                'lg:w-full lg:justify-start',
                            )}
                        >
                            <Icon className='h-4 w-4 shrink-0 opacity-70' aria-hidden />
                            {label}
                        </TabsTrigger>
                    ))}
                </TabsList>

                <div className='min-w-0 flex-1 space-y-6'>
                    <TabsContent value='profile' className='m-0 focus-visible:outline-none'>
                        <ProfileSettings />
                    </TabsContent>

                    <TabsContent value='preferences' className='m-0 focus-visible:outline-none'>
                        <PreferencesSettings />
                    </TabsContent>

                    <TabsContent value='integrations' className='m-0 focus-visible:outline-none'>
                        <IntegrationsSettings />
                    </TabsContent>

                    <TabsContent value='workspaces' className='m-0 focus-visible:outline-none'>
                        <WorkspaceSettings />
                    </TabsContent>
                </div>
            </Tabs>
        </div>
    );
}
