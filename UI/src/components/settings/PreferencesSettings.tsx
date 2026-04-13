import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { useTheme } from '@/components/theme-provider';
import { toast } from '@/hooks/use-toast';
import { useState, useEffect } from 'react';
import { Palette } from 'lucide-react';

const PREFERENCES_KEY = 'userPreferences';

type DateFormatId = 'mdy' | 'dmy' | 'ymd';

export function PreferencesSettings() {
    const { theme, setTheme } = useTheme();
    const [dateFormat, setDateFormat] = useState<DateFormatId>('mdy');

    useEffect(() => {
        try {
            const raw = localStorage.getItem(PREFERENCES_KEY);
            if (raw) {
                const parsed = JSON.parse(raw) as { dateFormat?: DateFormatId };
                if (parsed.dateFormat === 'mdy' || parsed.dateFormat === 'dmy' || parsed.dateFormat === 'ymd') {
                    setDateFormat(parsed.dateFormat);
                }
            }
        } catch {
            /* ignore corrupt localStorage */
        }
    }, []);

    function saveRegionalPreferences() {
        try {
            localStorage.setItem(PREFERENCES_KEY, JSON.stringify({ dateFormat }));
            toast({
                title: 'Preferences saved',
                description: 'Date format is stored on this device.',
            });
        } catch {
            toast({
                variant: 'destructive',
                title: 'Could not save',
                description: 'Storage may be disabled for this site.',
            });
        }
    }

    return (
        <Card className='overflow-hidden border-border/80 shadow-sm'>
            <CardHeader className='border-b border-border/60 bg-muted/20'>
                <div className='flex items-start gap-3'>
                    <div className='flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary'>
                        <Palette className='h-5 w-5' aria-hidden />
                    </div>
                    <div className='min-w-0 space-y-1'>
                        <CardTitle className='text-lg'>Display</CardTitle>
                        <CardDescription>
                            Theme applies immediately. Date format is saved in this browser only.
                        </CardDescription>
                    </div>
                </div>
            </CardHeader>
            <CardContent className='space-y-6 pt-6'>
                <div className='space-y-2'>
                    <Label htmlFor='settings-theme'>Theme</Label>
                    <Select value={theme} onValueChange={(value: 'light' | 'dark' | 'system') => setTheme(value)}>
                        <SelectTrigger id='settings-theme' className='max-w-md'>
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            <SelectItem value='light'>Light</SelectItem>
                            <SelectItem value='dark'>Dark</SelectItem>
                            <SelectItem value='system'>System</SelectItem>
                        </SelectContent>
                    </Select>
                </div>

                <Separator />

                <div className='space-y-4'>
                    <div className='space-y-2'>
                        <Label htmlFor='settings-date-format'>Date format</Label>
                        <Select value={dateFormat} onValueChange={value => setDateFormat(value as DateFormatId)}>
                            <SelectTrigger id='settings-date-format' className='max-w-md'>
                                <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value='mdy'>MM/DD/YYYY</SelectItem>
                                <SelectItem value='dmy'>DD/MM/YYYY</SelectItem>
                                <SelectItem value='ymd'>YYYY-MM-DD</SelectItem>
                            </SelectContent>
                        </Select>
                    </div>
                    <Button type='button' onClick={saveRegionalPreferences}>
                        Save date format
                    </Button>
                </div>
            </CardContent>
        </Card>
    );
}
