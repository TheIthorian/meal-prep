import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import type { MealPlanEntry, RecipeListItem, ShoppingList } from '@/models/meal-prep';
import { shoppingListsApi } from '@/lib/api';
import { formatDateLabel, mealTypeLabel } from '@/lib/meal-prep';

interface ShoppingListGeneratorDialogProps {
    workspaceId: string;
    recipes: RecipeListItem[];
    entries: MealPlanEntry[];
    onGenerated: (shoppingList: ShoppingList) => void;
}

export function ShoppingListGeneratorDialog({
    workspaceId,
    recipes,
    entries,
    onGenerated,
}: ShoppingListGeneratorDialogProps) {
    const [open, setOpen] = useState(false);
    const [isSaving, setSaving] = useState(false);
    const [name, setName] = useState('Weekly shop');
    const [notes, setNotes] = useState('');
    const [selectedRecipeIds, setSelectedRecipeIds] = useState<string[]>([]);
    const [selectedMealPlanEntryIds, setSelectedMealPlanEntryIds] = useState<string[]>(entries.map(entry => entry.id));

    const sortedEntries = useMemo(
        () =>
            [...entries].sort((left, right) =>
                `${left.plannedDate}-${left.mealType}`.localeCompare(`${right.plannedDate}-${right.mealType}`),
            ),
        [entries],
    );

    const toggleValue = (values: string[], value: string) =>
        values.includes(value) ? values.filter(currentValue => currentValue !== value) : [...values, value];

    const handleGenerate = async () => {
        setSaving(true);
        try {
            const shoppingList = await shoppingListsApi.generate(workspaceId, {
                name,
                notes: notes || null,
                recipeIds: selectedRecipeIds,
                mealPlanEntryIds: selectedMealPlanEntryIds,
            });

            onGenerated(shoppingList);
            setOpen(false);
        } finally {
            setSaving(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
                <Button>Generate Shopping List</Button>
            </DialogTrigger>
            <DialogContent className='max-h-[85vh] overflow-y-auto sm:max-w-2xl'>
                <DialogHeader>
                    <DialogTitle>Generate Shopping List</DialogTitle>
                </DialogHeader>

                <div className='space-y-5'>
                    <div className='grid gap-4 md:grid-cols-2'>
                        <div className='space-y-2'>
                            <label className='text-sm font-medium'>List Name</label>
                            <Input value={name} onChange={event => setName(event.target.value)} />
                        </div>
                        <div className='space-y-2 md:col-span-2'>
                            <label className='text-sm font-medium'>Notes</label>
                            <Textarea
                                value={notes}
                                onChange={event => setNotes(event.target.value)}
                                placeholder='Optional notes for this shopping trip.'
                            />
                        </div>
                    </div>

                    <div className='space-y-3'>
                        <h3 className='text-sm font-semibold'>Planned Meals</h3>
                        <div className='space-y-2'>
                            {sortedEntries.length === 0 && (
                                <p className='text-sm text-muted-foreground'>
                                    No meal-plan entries found for the current view.
                                </p>
                            )}
                            {sortedEntries.map(entry => (
                                <label
                                    key={entry.id}
                                    className='flex items-start gap-3 rounded-lg border border-border p-3'
                                >
                                    <Checkbox
                                        checked={selectedMealPlanEntryIds.includes(entry.id)}
                                        onCheckedChange={() =>
                                            setSelectedMealPlanEntryIds(currentValue =>
                                                toggleValue(currentValue, entry.id),
                                            )
                                        }
                                    />
                                    <div className='space-y-1'>
                                        <div className='font-medium'>{entry.recipeTitle}</div>
                                        <div className='text-sm text-muted-foreground'>
                                            {formatDateLabel(entry.plannedDate)} • {mealTypeLabel(entry.mealType)}
                                        </div>
                                    </div>
                                </label>
                            ))}
                        </div>
                    </div>

                    <div className='space-y-3'>
                        <h3 className='text-sm font-semibold'>Recipes</h3>
                        <div className='space-y-2'>
                            {recipes.map(recipe => (
                                <label
                                    key={recipe.id}
                                    className='flex items-start gap-3 rounded-lg border border-border p-3'
                                >
                                    <Checkbox
                                        checked={selectedRecipeIds.includes(recipe.id)}
                                        onCheckedChange={() =>
                                            setSelectedRecipeIds(currentValue => toggleValue(currentValue, recipe.id))
                                        }
                                    />
                                    <div className='space-y-1'>
                                        <div className='font-medium'>{recipe.title}</div>
                                        <div className='text-sm text-muted-foreground'>
                                            {recipe.ingredientCount} ingredients
                                        </div>
                                    </div>
                                </label>
                            ))}
                        </div>
                    </div>

                    <div className='flex justify-end'>
                        <Button
                            onClick={handleGenerate}
                            disabled={
                                isSaving || (selectedMealPlanEntryIds.length === 0 && selectedRecipeIds.length === 0)
                            }
                        >
                            {isSaving ? 'Generating...' : 'Generate List'}
                        </Button>
                    </div>
                </div>
            </DialogContent>
        </Dialog>
    );
}
