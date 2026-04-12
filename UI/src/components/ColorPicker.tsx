import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { Check } from 'lucide-react';

export const COLORS = [
    '#ef4444', // Red
    '#f97316', // Orange
    '#f59e0b', // Amber
    '#eab308', // Yellow
    '#84cc16', // Lime
    '#22c55e', // Green
    '#10b981', // Emerald
    '#14b8a6', // Teal
    '#06b6d4', // Cyan
    '#0ea5e9', // Sky
    '#3b82f6', // Blue
    '#6366f1', // Indigo
    '#8b5cf6', // Violet
    '#a855f7', // Purple
    '#d946ef', // Fuchsia
    '#ec4899', // Pink
    '#f43f5e', // Rose
    '#64748b', // Slate
];

interface ColorPickerProps {
    value?: string;
    onChange: (color: string) => void;
}

export function ColorPicker({ value, onChange }: ColorPickerProps) {
    return (
        <Popover>
            <PopoverTrigger asChild>
                <Button
                    variant='outline'
                    className={cn('w-full justify-start text-left font-normal', !value && 'text-muted-foreground')}
                >
                    <div className='w-full flex items-center gap-2'>
                        {value ? (
                            <div className='h-4 w-4 rounded-full border' style={{ backgroundColor: value }} />
                        ) : (
                            <div className='h-4 w-4 rounded-full bg-muted border' />
                        )}
                        <span>{value ? 'Selected Color' : 'Pick a color'}</span>
                    </div>
                </Button>
            </PopoverTrigger>
            <PopoverContent className='w-64'>
                <div className='grid grid-cols-6 gap-2'>
                    {COLORS.map(color => (
                        <div
                            key={color}
                            className={cn(
                                'h-8 w-8 rounded-md cursor-pointer border flex items-center justify-center transition-all hover:scale-110',
                                value === color ? 'ring-2 ring-primary ring-offset-2' : '',
                            )}
                            style={{ backgroundColor: color }}
                            onClick={() => onChange(color)}
                        >
                            {value === color && <Check className='h-4 w-4 text-white drop-shadow-md' />}
                        </div>
                    ))}
                </div>
            </PopoverContent>
        </Popover>
    );
}
