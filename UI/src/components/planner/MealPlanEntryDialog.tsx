import { useEffect, useState } from 'react';
import { ChevronsUpDown, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import type { MealPlanEntry, RecipeListItem } from '@/models/meal-prep';
import { mealPlanApi } from '@/lib/api';

interface MealPlanEntryDialogProps {
    workspaceId: string;
    recipes: RecipeListItem[];
    entry?: MealPlanEntry;
    selectedDate?: string;
    defaultMealType?: string;
    onSaved: (entry: MealPlanEntry) => void;
    onDeleted?: (entryId: string) => void;
    triggerLabel: string;
}

export function MealPlanEntryDialog({
    workspaceId,
    recipes,
    entry,
    selectedDate,
    onSaved,
    onDeleted,
    triggerLabel,
    defaultMealType,
}: MealPlanEntryDialogProps) {
    const [open, setOpen] = useState(false);
    const [isSaving, setSaving] = useState(false);
    const [recipeId, setRecipeId] = useState(entry?.recipeId ?? recipes[0]?.id ?? '');
    const [plannedDate, setPlannedDate] = useState(
        entry?.plannedDate ?? selectedDate ?? new Date().toISOString().slice(0, 10),
    );
    const [mealType, setMealType] = useState(entry?.mealType ?? defaultMealType ?? 'dinner');
    const [targetServings, setTargetServings] = useState<string>(entry?.targetServings?.toString() ?? '');
    const [notes, setNotes] = useState(entry?.notes ?? '');
    const [status, setStatus] = useState(entry?.status ?? 'planned');
    const [recipePickerOpen, setRecipePickerOpen] = useState(false);
    const [completedAtUtc, setCompletedAtUtc] = useState<string>(() =>
        entry?.completedAtUtc ? entry.completedAtUtc.slice(0, 10) : '',
    );
    const selectedRecipe = recipes.find(recipe => recipe.id === recipeId);

    useEffect(() => {
        if (!open) return;
        setRecipeId(entry?.recipeId ?? recipes[0]?.id ?? '');
        setPlannedDate(entry?.plannedDate ?? selectedDate ?? new Date().toISOString().slice(0, 10));
        setMealType(entry?.mealType ?? defaultMealType ?? 'dinner');
        setTargetServings(entry?.targetServings?.toString() ?? '');
        setNotes(entry?.notes ?? '');
        setStatus(entry?.status ?? 'planned');
        setCompletedAtUtc(entry?.completedAtUtc ? entry.completedAtUtc.slice(0, 10) : '');
    }, [defaultMealType, entry, open, recipes, selectedDate]);

    const handleSave = async () => {
        setSaving(true);
        try {
            const payload = {
                recipeId,
                plannedDate,
                mealType,
                targetServings: targetServings === '' ? null : Number(targetServings),
                notes: notes || null,
                status,
                completedAtUtc:
                    status === 'completed'
                        ? completedAtUtc
                            ? `${completedAtUtc}T12:00:00Z`
                            : new Date().toISOString()
                        : null,
            };

            const savedEntry = entry
                ? await mealPlanApi.update(workspaceId, entry.id, payload)
                : await mealPlanApi.create(workspaceId, payload);

            onSaved(savedEntry);
            setOpen(false);
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async () => {
        if (!entry || !onDeleted) return;
        await mealPlanApi.remove(workspaceId, entry.id);
        onDeleted(entry.id);
        setOpen(false);
    };
    const triggerAriaLabel = triggerLabel || 'Add meal';
    const triggerButton = (
        <Button
            variant={entry ? 'ghost' : 'outline'}
            size={triggerLabel ? 'default' : 'icon'}
            className={triggerLabel ? undefined : 'h-8 w-full text-muted-foreground/40 hover:text-primary'}
            aria-label={triggerAriaLabel}
        >
            {triggerLabel ? triggerLabel : <Plus className='h-4 w-4' />}
        </Button>
    );

    return (
        <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
                {triggerLabel ? (
                    triggerButton
                ) : (
                    <Tooltip>
                        <TooltipTrigger asChild>{triggerButton}</TooltipTrigger>
                        <TooltipContent>{triggerAriaLabel}</TooltipContent>
                    </Tooltip>
                )}
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>{entry ? 'Edit next meal' : 'Add next meal'}</DialogTitle>
                </DialogHeader>

                <div className='space-y-4'>
                    <div className='space-y-2'>
                        <label className='text-sm font-medium'>Recipe</label>
                        <Popover open={recipePickerOpen} onOpenChange={setRecipePickerOpen}>
                            <PopoverTrigger asChild>
                                <Button
                                    type='button'
                                    variant='outline'
                                    role='combobox'
                                    aria-expanded={recipePickerOpen}
                                    className='w-full justify-between'
                                    disabled={recipes.length === 0}
                                >
                                    <span className='truncate text-left'>
                                        {selectedRecipe?.title ?? 'Search recipes...'}
                                    </span>
                                    <ChevronsUpDown className='ml-2 h-4 w-4 shrink-0 opacity-50' />
                                </Button>
                            </PopoverTrigger>
                            <PopoverContent className='w-[var(--radix-popover-trigger-width)] p-0' align='start'>
                                <Command>
                                    <CommandInput placeholder='Search recipes...' />
                                    <CommandList>
                                        <CommandEmpty>No matching recipes</CommandEmpty>
                                        <CommandGroup>
                                            {recipes.map(recipe => (
                                                <CommandItem
                                                    key={recipe.id}
                                                    value={`${recipe.title} ${recipe.description ?? ''}`}
                                                    onSelect={() => {
                                                        setRecipeId(recipe.id);
                                                        setRecipePickerOpen(false);
                                                    }}
                                                >
                                                    <span className='truncate'>{recipe.title}</span>
                                                </CommandItem>
                                            ))}
                                        </CommandGroup>
                                    </CommandList>
                                </Command>
                            </PopoverContent>
                        </Popover>
                    </div>
                    {entry && (
                        <>
                            <div className='grid gap-4 md:grid-cols-2'>
                                <div className='space-y-2'>
                                    <label className='text-sm font-medium'>Target Date</label>
                                    <Input
                                        type='date'
                                        value={plannedDate}
                                        onChange={event => setPlannedDate(event.target.value)}
                                    />
                                </div>
                                <div className='space-y-2'>
                                    <label className='text-sm font-medium'>Meal Type</label>
                                    <select
                                        className='flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm'
                                        value={mealType}
                                        onChange={event => setMealType(event.target.value)}
                                    >
                                        {['breakfast', 'lunch', 'dinner', 'snack'].map(value => (
                                            <option key={value} value={value}>
                                                {value.charAt(0).toUpperCase() + value.slice(1)}
                                            </option>
                                        ))}
                                    </select>
                                </div>
                            </div>
                            <div className='grid gap-4 md:grid-cols-2'>
                                <div className='space-y-2'>
                                    <label className='text-sm font-medium'>Target Servings</label>
                                    <Input
                                        type='number'
                                        min='1'
                                        step='0.5'
                                        value={targetServings}
                                        onChange={event => setTargetServings(event.target.value)}
                                    />
                                </div>
                                <div className='space-y-2'>
                                    <label className='text-sm font-medium'>Status</label>
                                    <select
                                        className='flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm'
                                        value={status}
                                        onChange={event => setStatus(event.target.value)}
                                    >
                                        <option value='planned'>Planned</option>
                                        <option value='completed'>Completed</option>
                                    </select>
                                </div>
                            </div>
                            {status === 'completed' && (
                                <div className='space-y-2'>
                                    <label className='text-sm font-medium'>Completed On</label>
                                    <Input
                                        type='date'
                                        value={completedAtUtc}
                                        onChange={event => setCompletedAtUtc(event.target.value)}
                                    />
                                </div>
                            )}
                            <div className='space-y-2'>
                                <label className='text-sm font-medium'>Notes</label>
                                <Textarea
                                    value={notes}
                                    onChange={event => setNotes(event.target.value)}
                                    placeholder='Optional notes for this meal.'
                                />
                            </div>
                        </>
                    )}
                    <div className='flex items-center justify-between gap-3'>
                        {entry && onDeleted ? (
                            <Button variant='outline' onClick={handleDelete}>
                                Delete
                            </Button>
                        ) : (
                            <span />
                        )}
                        <Button onClick={handleSave} disabled={!recipeId || isSaving}>
                            {isSaving ? 'Saving...' : entry ? 'Save Changes' : 'Add to Next Meals'}
                        </Button>
                    </div>
                </div>
            </DialogContent>
        </Dialog>
    );
}
