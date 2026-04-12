import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useState, useEffect } from 'react';
import { useQueryString } from '@/hooks/use-query-string';
import { ProfileSettings } from '@/components/settings/ProfileSettings';
import { PreferencesSettings } from '@/components/settings/PreferencesSettings';
import { WorkspaceSettings } from '@/components/settings/WorkspaceSettings';

export default function Settings() {
    const { params, setParam } = useQueryString(['tab']);
    const [activeTab, setActiveTab] = useState(params.tab || 'profile');

    // Update activeTab when URL changes
    useEffect(() => {
        if (params.tab) {
            setActiveTab(params.tab);
        }
    }, [params.tab]);

    // Update URL when tab changes
    const handleTabChange = (value: string) => {
        setActiveTab(value);
        setParam('tab', value);
    };

    return (
        <div className='min-w-0 space-y-6 animate-fade-in'>
            <div>
                <h1 className='text-2xl font-bold tracking-tight sm:text-3xl'>Settings</h1>
                <p className='text-muted-foreground mt-1'>Manage your account and workspace preferences</p>
            </div>

            <Tabs value={activeTab} onValueChange={handleTabChange} className='space-y-4'>
                <TabsList className='w-full flex-wrap justify-start gap-1 overflow-x-auto sm:flex-nowrap sm:overflow-visible'>
                    <TabsTrigger value='profile' className='flex-shrink-0'>
                        Profile
                    </TabsTrigger>
                    <TabsTrigger value='preferences' className='flex-shrink-0'>
                        Preferences
                    </TabsTrigger>
                    <TabsTrigger value='workspaces' className='flex-shrink-0'>
                        Workspaces
                    </TabsTrigger>
                </TabsList>

                <TabsContent value='profile' className='space-y-4'>
                    <ProfileSettings />
                </TabsContent>

                <TabsContent value='preferences' className='space-y-4'>
                    <PreferencesSettings />
                </TabsContent>

                <TabsContent value='workspaces' className='space-y-4'>
                    <WorkspaceSettings />
                </TabsContent>
            </Tabs>
        </div>
    );
}
