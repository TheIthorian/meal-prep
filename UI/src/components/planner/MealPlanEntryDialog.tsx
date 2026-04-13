import { useEffect, useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
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

    useEffect(() => {
        if (!open) return;
        setRecipeId(entry?.recipeId ?? recipes[0]?.id ?? '');
        setPlannedDate(entry?.plannedDate ?? selectedDate ?? new Date().toISOString().slice(0, 10));
        setMealType(entry?.mealType ?? defaultMealType ?? 'dinner');
        setTargetServings(entry?.targetServings?.toString() ?? '');
        setNotes(entry?.notes ?? '');
        setStatus(entry?.status ?? 'planned');
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

    return (
        <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
                <Button
                    variant={entry ? 'ghost' : 'outline'}
                    size={triggerLabel ? 'default' : 'icon'}
                    className={triggerLabel ? undefined : 'h-8 w-full text-muted-foreground/40 hover:text-primary'}
                    aria-label={triggerLabel || 'Add meal'}
                >
                    {triggerLabel ? triggerLabel : <Plus className='h-4 w-4' />}
                </Button>
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>{entry ? 'Edit planned meal' : 'Plan a meal'}</DialogTitle>
                </DialogHeader>

                <div className='space-y-4'>
                    <div className='space-y-2'>
                        <label className='text-sm font-medium'>Recipe</label>
                        <select
                            className='flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm'
                            value={recipeId}
                            onChange={event => setRecipeId(event.target.value)}
                        >
                            {recipes.map(recipe => (
                                <option key={recipe.id} value={recipe.id}>
                                    {recipe.title}
                                </option>
                            ))}
                        </select>
                    </div>
                    <div className='grid gap-4 md:grid-cols-2'>
                        <div className='space-y-2'>
                            <label className='text-sm font-medium'>Date</label>
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
                    <div className='space-y-2'>
                        <label className='text-sm font-medium'>Notes</label>
                        <Textarea
                            value={notes}
                            onChange={event => setNotes(event.target.value)}
                            placeholder='Optional notes for this meal.'
                        />
                    </div>
                    <div className='flex items-center justify-between gap-3'>
                        {entry && onDeleted ? (
                            <Button variant='outline' onClick={handleDelete}>
                                Delete
                            </Button>
                        ) : (
                            <span />
                        )}
                        <Button onClick={handleSave} disabled={!recipeId || isSaving}>
                            {isSaving ? 'Saving...' : entry ? 'Save Changes' : 'Add Meal'}
                        </Button>
                    </div>
                </div>
            </DialogContent>
        </Dialog>
    );
}
