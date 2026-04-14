import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import type { Recipe } from '@/models/meal-prep';
import { buildIngredientDisplay, getNutrientAmount, setNutrientAmount, setServingBasis } from '@/lib/meal-prep';
import { RecipeTagsField } from '@/components/recipes/RecipeTagsField';
import { Trash2 } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

interface RecipeFormProps {
    recipe: Recipe;
    isSaving?: boolean;
    onChange: (recipe: Recipe) => void;
    onSubmit: () => void;
    onDelete?: () => void;
    /** When set, tags are chosen from the server whitelist with optional AI suggestions. */
    workspaceId?: string;
}

export function RecipeForm({ recipe, isSaving, onChange, onSubmit, onDelete, workspaceId }: RecipeFormProps) {
    const updateRecipe = <K extends keyof Recipe>(key: K, value: Recipe[K]) => {
        onChange({ ...recipe, [key]: value });
    };

    const updateIngredient = (index: number, field: string, value: string) => {
        const nextIngredients = [...recipe.ingredients];
        const currentIngredient = nextIngredients[index];
        const numericValue = field === 'amount' ? (value === '' ? null : Number(value)) : value;

        nextIngredients[index] = {
            ...currentIngredient,
            [field]: numericValue,
        };
        nextIngredients[index].displayText = buildIngredientDisplay({
            amount: nextIngredients[index].amount,
            unit: nextIngredients[index].unit,
            name: nextIngredients[index].name,
            preparationNote: nextIngredients[index].preparationNote,
        });

        onChange({ ...recipe, ingredients: nextIngredients });
    };

    const updateStep = (index: number, field: 'instruction' | 'timerSeconds', value: string) => {
        const nextSteps = [...recipe.steps];
        nextSteps[index] = {
            ...nextSteps[index],
            [field]: field === 'timerSeconds' ? (value === '' ? null : Number(value)) : value,
        };

        onChange({ ...recipe, steps: nextSteps });
    };

    const tagsValue = recipe.tags.join(', ');

    return (
        <div className='space-y-6'>
            <Card>
                <CardHeader>
                    <CardTitle>Recipe Details</CardTitle>
                </CardHeader>
                <CardContent className='space-y-4'>
                    <div className='grid gap-4 md:grid-cols-2'>
                        <div className='space-y-2 md:col-span-2'>
                            <label className='text-sm font-medium'>Title</label>
                            <Input
                                value={recipe.title}
                                onChange={event => updateRecipe('title', event.target.value)}
                                placeholder='Creamy tomato pasta'
                            />
                        </div>
                        <div className='space-y-2 md:col-span-2'>
                            <label className='text-sm font-medium'>Description</label>
                            <Textarea
                                value={recipe.description ?? ''}
                                onChange={event => updateRecipe('description', event.target.value)}
                                placeholder='Why you like this recipe, key flavors, or when you make it.'
                            />
                        </div>
                        <div className='space-y-2'>
                            <label className='text-sm font-medium'>Servings</label>
                            <Input
                                type='number'
                                min='1'
                                step='0.5'
                                value={recipe.servings}
                                onChange={event => updateRecipe('servings', Number(event.target.value))}
                            />
                        </div>
                        <div className='space-y-2'>
                            <label className='text-sm font-medium'>Source URL</label>
                            <Input
                                value={recipe.sourceUrl ?? ''}
                                onChange={event => updateRecipe('sourceUrl', event.target.value)}
                                placeholder='https://example.com/recipe'
                            />
                        </div>
                        <div className='space-y-2'>
                            <label className='text-sm font-medium'>Prep Minutes</label>
                            <Input
                                type='number'
                                min='0'
                                value={recipe.prepMinutes ?? ''}
                                onChange={event =>
                                    updateRecipe(
                                        'prepMinutes',
                                        event.target.value === '' ? null : Number(event.target.value),
                                    )
                                }
                            />
                        </div>
                        <div className='space-y-2'>
                            <label className='text-sm font-medium'>Cook Minutes</label>
                            <Input
                                type='number'
                                min='0'
                                value={recipe.cookMinutes ?? ''}
                                onChange={event =>
                                    updateRecipe(
                                        'cookMinutes',
                                        event.target.value === '' ? null : Number(event.target.value),
                                    )
                                }
                            />
                        </div>
                        <div className='space-y-2 md:col-span-2'>
                            {workspaceId ? (
                                <RecipeTagsField
                                    workspaceId={workspaceId}
                                    selectedTags={recipe.tags}
                                    onChange={tags => updateRecipe('tags', tags)}
                                    title={recipe.title}
                                    description={recipe.description}
                                    ingredientNames={recipe.ingredients.map(i => i.name).filter(Boolean)}
                                    stepInstructions={recipe.steps.map(s => s.instruction).filter(Boolean)}
                                />
                            ) : (
                                <>
                                    <label className='text-sm font-medium'>Tags</label>
                                    <Input
                                        value={tagsValue}
                                        onChange={event =>
                                            updateRecipe(
                                                'tags',
                                                event.target.value
                                                    .split(',')
                                                    .map(value => value.trim())
                                                    .filter(Boolean),
                                            )
                                        }
                                        placeholder='dinner, quick, vegetarian (kebab-case from server whitelist)'
                                    />
                                </>
                            )}
                        </div>
                        <div className='space-y-2 md:col-span-2'>
                            <label className='text-sm font-medium'>Notes</label>
                            <Textarea
                                value={recipe.notes ?? ''}
                                onChange={event => updateRecipe('notes', event.target.value)}
                                placeholder='Substitutions, serving notes, or reminders.'
                            />
                        </div>
                    </div>
                </CardContent>
            </Card>

            <Card>
                <CardHeader className='flex flex-row items-center justify-between gap-4'>
                    <CardTitle>Ingredients</CardTitle>
                    <Button
                        variant='outline'
                        onClick={() =>
                            onChange({
                                ...recipe,
                                ingredients: [
                                    ...recipe.ingredients,
                                    {
                                        id: crypto.randomUUID(),
                                        sortOrder: recipe.ingredients.length,
                                        name: '',
                                        amount: null,
                                        unit: '',
                                        normalizedIngredientName: '',
                                        preparationNote: '',
                                        section: '',
                                        displayText: '',
                                    },
                                ],
                            })
                        }
                    >
                        Add Ingredient
                    </Button>
                </CardHeader>
                <CardContent className='space-y-4'>
                    {recipe.ingredients.map((ingredient, index) => (
                        <div
                            key={ingredient.id || `${ingredient.name}-${index}`}
                            className='rounded-xl border border-border p-4'
                        >
                            <div className='grid gap-3 md:grid-cols-12'>
                                <div className='space-y-2 md:col-span-2'>
                                    <label className='text-sm font-medium'>Amount</label>
                                    <Input
                                        type='number'
                                        step='0.25'
                                        value={ingredient.amount ?? ''}
                                        onChange={event => updateIngredient(index, 'amount', event.target.value)}
                                    />
                                </div>
                                <div className='space-y-2 md:col-span-2'>
                                    <label className='text-sm font-medium'>Unit</label>
                                    <Input
                                        value={ingredient.unit ?? ''}
                                        onChange={event => updateIngredient(index, 'unit', event.target.value)}
                                        placeholder='g, ml, tbsp'
                                    />
                                </div>
                                <div className='space-y-2 md:col-span-4'>
                                    <label className='text-sm font-medium'>Ingredient</label>
                                    <Input
                                        value={ingredient.name}
                                        onChange={event => updateIngredient(index, 'name', event.target.value)}
                                        placeholder='Cherry tomatoes'
                                    />
                                </div>
                                <div className='space-y-2 md:col-span-3'>
                                    <label className='text-sm font-medium'>Preparation Note</label>
                                    <Input
                                        value={ingredient.preparationNote ?? ''}
                                        onChange={event =>
                                            updateIngredient(index, 'preparationNote', event.target.value)
                                        }
                                        placeholder='halved'
                                    />
                                </div>
                                <div className='flex items-end md:col-span-1'>
                                    <Tooltip>
                                        <TooltipTrigger asChild>
                                            <Button
                                                variant='ghost'
                                                size='icon'
                                                aria-label='Remove ingredient'
                                                onClick={() =>
                                                    onChange({
                                                        ...recipe,
                                                        ingredients: recipe.ingredients.filter(
                                                            (_, ingredientIndex) => ingredientIndex !== index,
                                                        ),
                                                    })
                                                }
                                            >
                                                <Trash2 className='h-4 w-4' />
                                            </Button>
                                        </TooltipTrigger>
                                        <TooltipContent side='bottom'>Remove ingredient</TooltipContent>
                                    </Tooltip>
                                </div>
                            </div>
                        </div>
                    ))}
                </CardContent>
            </Card>

            <Card>
                <CardHeader className='flex flex-row items-center justify-between gap-4'>
                    <CardTitle>Steps</CardTitle>
                    <Button
                        variant='outline'
                        onClick={() =>
                            onChange({
                                ...recipe,
                                steps: [
                                    ...recipe.steps,
                                    {
                                        id: crypto.randomUUID(),
                                        sortOrder: recipe.steps.length,
                                        instruction: '',
                                        timerSeconds: null,
                                    },
                                ],
                            })
                        }
                    >
                        Add Step
                    </Button>
                </CardHeader>
                <CardContent className='space-y-4'>
                    {recipe.steps.map((step, index) => (
                        <div key={step.id || `${index}`} className='rounded-xl border border-border p-4'>
                            <div className='grid gap-3 md:grid-cols-12'>
                                <div className='space-y-2 md:col-span-10'>
                                    <label className='text-sm font-medium'>Instruction</label>
                                    <Textarea
                                        value={step.instruction}
                                        onChange={event => updateStep(index, 'instruction', event.target.value)}
                                        placeholder='Cook the pasta until al dente.'
                                    />
                                </div>
                                <div className='space-y-2 md:col-span-1'>
                                    <label className='text-sm font-medium'>Timer</label>
                                    <Input
                                        type='number'
                                        min='0'
                                        value={step.timerSeconds ?? ''}
                                        onChange={event => updateStep(index, 'timerSeconds', event.target.value)}
                                    />
                                </div>
                                <div className='flex items-end md:col-span-1'>
                                    <Tooltip>
                                        <TooltipTrigger asChild>
                                            <Button
                                                variant='ghost'
                                                size='icon'
                                                aria-label='Remove step'
                                                onClick={() =>
                                                    onChange({
                                                        ...recipe,
                                                        steps: recipe.steps.filter((_, stepIndex) => stepIndex !== index),
                                                    })
                                                }
                                            >
                                                <Trash2 className='h-4 w-4' />
                                            </Button>
                                        </TooltipTrigger>
                                        <TooltipContent side='bottom'>Remove step</TooltipContent>
                                    </Tooltip>
                                </div>
                            </div>
                        </div>
                    ))}
                </CardContent>
            </Card>

            <Card>
                <CardHeader>
                    <CardTitle>Nutrition</CardTitle>
                </CardHeader>
                <CardContent className='grid gap-4 md:grid-cols-4'>
                    {[
                        { label: 'Calories', nutrientType: 'calories' },
                        { label: 'Protein (g)', nutrientType: 'protein' },
                        { label: 'Carbs (g)', nutrientType: 'carbohydrate' },
                        { label: 'Fat (g)', nutrientType: 'fat' },
                        { label: 'Fiber (g)', nutrientType: 'fiber' },
                        { label: 'Sugar (g)', nutrientType: 'sugar' },
                        { label: 'Sodium (mg)', nutrientType: 'sodium' },
                    ].map(({ label, nutrientType }) => (
                        <div key={nutrientType} className='space-y-2'>
                            <label className='text-sm font-medium'>{label}</label>
                            <Input
                                type='number'
                                min='0'
                                step='0.1'
                                value={getNutrientAmount(recipe.nutrition, nutrientType) ?? ''}
                                onChange={event =>
                                    onChange(
                                        setNutrientAmount(
                                            recipe,
                                            nutrientType,
                                            event.target.value === '' ? null : Number(event.target.value),
                                        ),
                                    )
                                }
                            />
                        </div>
                    ))}
                    <div className='space-y-2'>
                        <label className='text-sm font-medium'>Serving basis</label>
                        <Input
                            type='number'
                            min='0'
                            step='0.1'
                            value={recipe.nutrition?.servingBasis ?? ''}
                            onChange={event =>
                                onChange(
                                    setServingBasis(
                                        recipe,
                                        event.target.value === '' ? null : Number(event.target.value),
                                    ),
                                )
                            }
                        />
                    </div>
                </CardContent>
            </Card>

            <div className='flex flex-wrap items-center justify-between gap-3'>
                <div className='flex gap-3'>
                    <Button onClick={onSubmit} disabled={isSaving}>
                        {isSaving ? 'Saving...' : 'Save Recipe'}
                    </Button>
                    {onDelete && (
                        <Button variant='outline' onClick={onDelete}>
                            Delete
                        </Button>
                    )}
                </div>
            </div>
        </div>
    );
}
