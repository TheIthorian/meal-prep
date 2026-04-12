import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useTheme } from '@/components/theme-provider';
import { useToast } from '@/components/ui/use-toast';
import { useState, useEffect } from 'react';

export function PreferencesSettings() {
    const { theme, setTheme } = useTheme();
    const { toast } = useToast();
    const [preferences, setPreferences] = useState({
        currency: 'usd',
        dateFormat: 'mdy',
    });

    // Load preferences from localStorage
    useEffect(() => {
        const saved = localStorage.getItem('userPreferences');
        if (saved) {
            setPreferences(JSON.parse(saved));
        }
    }, []);

    // Save preferences to localStorage
    const savePreferences = () => {
        localStorage.setItem('userPreferences', JSON.stringify(preferences));
        toast({
            title: 'Preferences saved',
            description: 'Your preferences have been saved locally.',
        });
    };

    return (
        <Card>
            <CardHeader>
                <CardTitle>Display Preferences</CardTitle>
                <CardDescription>Customize how you view your data (saved locally)</CardDescription>
            </CardHeader>
            <CardContent className='space-y-4'>
                <div className='space-y-2'>
                    <Label htmlFor='dateFormat'>Date Format</Label>
                    <Select
                        value={preferences.dateFormat}
                        onValueChange={value => setPreferences({ ...preferences, dateFormat: value })}
                    >
                        <SelectTrigger id='dateFormat'>
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            <SelectItem value='mdy'>MM/DD/YYYY</SelectItem>
                            <SelectItem value='dmy'>DD/MM/YYYY</SelectItem>
                            <SelectItem value='ymd'>YYYY-MM-DD</SelectItem>
                        </SelectContent>
                    </Select>
                </div>
                <div className='space-y-2'>
                    <Label htmlFor='theme'>Theme</Label>
                    <Select value={theme} onValueChange={(value: 'light' | 'dark' | 'system') => setTheme(value)}>
                        <SelectTrigger id='theme'>
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            <SelectItem value='light'>Light</SelectItem>
                            <SelectItem value='dark'>Dark</SelectItem>
                            <SelectItem value='system'>System</SelectItem>
                        </SelectContent>
                    </Select>
                </div>
                <Button onClick={savePreferences}>Save Preferences</Button>
            </CardContent>
        </Card>
    );
}
