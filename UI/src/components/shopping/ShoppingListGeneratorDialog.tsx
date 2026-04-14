import { useEffect, useMemo, useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import type { MealPlanEntry, RecipeListItem, ShoppingList } from '@/models/meal-prep';
import { shoppingListsApi } from '@/lib/api';
import { formatDateLabel, mealTypeLabel } from '@/lib/meal-prep';

interface ShoppingListGeneratorDialogProps {
    workspaceId: string;
    recipes: RecipeListItem[];
    entries: MealPlanEntry[];
    onGenerated: (shoppingList: ShoppingList) => void;
    triggerLabel?: string;
}

function formatLongLocaleDate(date: Date) {
    return new Intl.DateTimeFormat(undefined, {
        weekday: 'long',
        day: 'numeric',
        month: 'long',
    }).format(date);
}

export function ShoppingListGeneratorDialog({
    workspaceId,
    recipes,
    entries,
    onGenerated,
    triggerLabel = 'Generate Shopping List',
}: ShoppingListGeneratorDialogProps) {
    const defaultListName = useMemo(() => formatLongLocaleDate(new Date()), []);
    const [open, setOpen] = useState(false);
    const [isSaving, setSaving] = useState(false);
    const [recipePickerOpen, setRecipePickerOpen] = useState(false);
    const [name, setName] = useState(defaultListName);
    const [notes, setNotes] = useState('');
    const [selectedRecipeIds, setSelectedRecipeIds] = useState<string[]>([]);
    const selectableEntries = useMemo(
        () => entries.filter(entry => entry.status !== 'completed').slice(0, 50),
        [entries],
    );
    const [selectedNextMealIds, setSelectedNextMealIds] = useState<string[]>(selectableEntries.map(entry => entry.id));

    const sortedEntries = useMemo(
        () =>
            [...selectableEntries].sort((left, right) =>
                `${left.plannedDate}-${left.mealType}`.localeCompare(`${right.plannedDate}-${right.mealType}`),
            ),
        [selectableEntries],
    );

    useEffect(() => {
        setSelectedNextMealIds(selectableEntries.map(entry => entry.id));
    }, [selectableEntries, open]);

    useEffect(() => {
        if (!open) return;
        setName(formatLongLocaleDate(new Date()));
    }, [open]);

    const toggleValue = (values: string[], value: string) =>
        values.includes(value) ? values.filter(currentValue => currentValue !== value) : [...values, value];

    const handleGenerate = async () => {
        setSaving(true);
        try {
            const shoppingList = await shoppingListsApi.generate(workspaceId, {
                name,
                notes: notes || null,
                recipeIds: selectedRecipeIds,
                nextMealIds: selectedNextMealIds,
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
                <Button>{triggerLabel}</Button>
            </DialogTrigger>
            <DialogContent className='flex h-[90vh] max-h-[90vh] w-[calc(100vw-2rem)] max-w-2xl flex-col overflow-hidden p-0'>
                <DialogHeader className='px-6 pt-6'>
                    <DialogTitle>Generate Shopping List</DialogTitle>
                </DialogHeader>

                <div className='min-h-0 flex-1 space-y-5 overflow-y-auto px-6 pb-6'>
                    <div className='grid gap-4 md:grid-cols-2'>
                        <div className='space-y-2'>
                            <label className='text-sm font-medium'>List Name</label>
                            <Input value={name} onChange={event => setName(event.target.value)} />
                        </div>
                        <div className='space-y-2 md:col-span-2'>
                            <label className='text-sm font-medium'>Notes</label>
                            <Textarea
                                className='resize-y max-w-full'
                                value={notes}
                                onChange={event => setNotes(event.target.value)}
                                placeholder='Optional notes for this shopping trip.'
                            />
                        </div>
                    </div>

                    <div className='space-y-3'>
                        <h3 className='text-sm font-semibold'>From Next Meals</h3>
                        <div className='space-y-2'>
                            {sortedEntries.length === 0 && (
                                <p className='text-sm text-muted-foreground'>
                                    No next meals found yet.
                                </p>
                            )}
                            {sortedEntries.map(entry => (
                                <label key={entry.id} className='flex items-start gap-3 rounded-lg border border-border p-3'>
                                    <Checkbox
                                        checked={selectedNextMealIds.includes(entry.id)}
                                        onCheckedChange={() =>
                                            setSelectedNextMealIds(currentValue =>
                                                toggleValue(currentValue, entry.id),
                                            )
                                        }
                                    />
                                    <div className='min-w-0 space-y-1'>
                                        <div className='break-words text-sm font-medium'>{entry.recipeTitle}</div>
                                        <div className='break-words text-sm text-muted-foreground'>
                                            {formatDateLabel(entry.plannedDate)} • {mealTypeLabel(entry.mealType)}
                                        </div>
                                    </div>
                                </label>
                            ))}
                        </div>
                    </div>

                    <div className='space-y-3'>
                        <h3 className='text-sm font-semibold'>Recipes</h3>
                        <Popover open={recipePickerOpen} onOpenChange={setRecipePickerOpen}>
                            <PopoverTrigger asChild>
                                <Button
                                    type='button'
                                    variant='outline'
                                    role='combobox'
                                    aria-expanded={recipePickerOpen}
                                    className='w-full justify-between'
                                >
                                    <span className='truncate text-left'>
                                        {selectedRecipeIds.length === 0
                                            ? 'Search recipes to include...'
                                            : `${selectedRecipeIds.length} recipe${selectedRecipeIds.length === 1 ? '' : 's'} selected`}
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
                                                    onSelect={() =>
                                                        setSelectedRecipeIds(currentValue =>
                                                            toggleValue(currentValue, recipe.id),
                                                        )
                                                    }
                                                >
                                                    <Check
                                                        className={`mr-2 h-4 w-4 ${
                                                            selectedRecipeIds.includes(recipe.id)
                                                                ? 'opacity-100'
                                                                : 'opacity-0'
                                                        }`}
                                                    />
                                                    <span className='truncate'>{recipe.title}</span>
                                                </CommandItem>
                                            ))}
                                        </CommandGroup>
                                    </CommandList>
                                </Command>
                            </PopoverContent>
                        </Popover>
                        <div className='space-y-2'>
                            {selectedRecipeIds.length === 0 ? (
                                <p className='text-sm text-muted-foreground'>No recipes selected yet.</p>
                            ) : (
                                recipes
                                    .filter(recipe => selectedRecipeIds.includes(recipe.id))
                                    .map(recipe => (
                                        <label
                                            key={recipe.id}
                                            className='flex items-start gap-3 rounded-lg border border-border p-3'
                                        >
                                            <Checkbox
                                                checked
                                                onCheckedChange={() =>
                                                    setSelectedRecipeIds(currentValue =>
                                                        currentValue.filter(id => id !== recipe.id),
                                                    )
                                                }
                                            />
                                            <div className='min-w-0 space-y-1'>
                                                <div className='break-words text-sm font-medium'>{recipe.title}</div>
                                                <div className='break-words text-sm text-muted-foreground'>
                                                    {recipe.ingredientCount} ingredients
                                                </div>
                                            </div>
                                        </label>
                                    ))
                            )}
                        </div>
                    </div>

                </div>
                <div className='border-t border-border px-6 py-4'>
                    <div className='flex justify-end'>
                        <Button
                            onClick={handleGenerate}
                            disabled={isSaving || (selectedNextMealIds.length === 0 && selectedRecipeIds.length === 0)}
                        >
                            {isSaving ? 'Generating...' : 'Generate List'}
                        </Button>
                    </div>
                </div>
            </DialogContent>
        </Dialog>
    );
}
