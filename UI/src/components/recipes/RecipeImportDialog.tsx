import { type ReactNode, useState } from 'react';
import { recipesApi } from '@/lib/api';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { Recipe } from '@/models/meal-prep';
import { createEmptyRecipe } from '@/lib/meal-prep';

interface RecipeImportDialogProps {
    workspaceId: string;
    onImported: (recipe: Recipe) => void;
    trigger?: ReactNode;
}

export function RecipeImportDialog({ workspaceId, onImported, trigger }: RecipeImportDialogProps) {
    const [open, setOpen] = useState(false);
    const [url, setUrl] = useState('');
    const [isLoading, setLoading] = useState(false);
    const [isSaving, setSaving] = useState(false);
    const [previewTitle, setPreviewTitle] = useState<string | null>(null);

    const handlePreview = async () => {
        setLoading(true);
        try {
            const preview = await recipesApi.previewImport(workspaceId, url);
            setPreviewTitle(preview.title);
            const recipe = createEmptyRecipe(workspaceId);
            onImported({
                ...recipe,
                title: preview.title,
                description: preview.description ?? '',
                servings: preview.servings,
                sourceUrl: preview.sourceUrl,
                importImageUrl: preview.imageUrl ?? null,
                prepMinutes: preview.prepMinutes ?? null,
                cookMinutes: preview.cookMinutes ?? null,
                tags: preview.tags,
                ingredients: preview.ingredients.map((ingredient, index) => ({
                    ...ingredient,
                    id: ingredient.id || `${index}`,
                })),
                steps: preview.steps.map((step, index) => ({ ...step, id: step.id || `${index}` })),
                nutrition: preview.nutrition
                    ? {
                          servingBasis: preview.nutrition.servingBasis ?? null,
                          nutrients: preview.nutrition.nutrients.map(n => ({
                              id: n.id || crypto.randomUUID(),
                              nutrientType: n.nutrientType,
                              amount: Number(n.amount),
                          })),
                      }
                    : null,
            });
        } finally {
            setLoading(false);
            setSaving(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
                {trigger ?? <Button variant='outline'>Import From URL</Button>}
            </DialogTrigger>
            <DialogContent className='sm:max-w-xl'>
                <DialogHeader>
                    <DialogTitle>Import Recipe From Web</DialogTitle>
                    <DialogDescription>Paste a recipe URL. Meal Prep will preview the recipe so you can review and edit it.</DialogDescription>
                </DialogHeader>

                <div className='space-y-4'>
                    <Input value={url} onChange={event => setUrl(event.target.value)} placeholder='https://example.com/recipe' />
                    <Button onClick={handlePreview} disabled={!url || isLoading || isSaving}>
                        {isLoading ? 'Fetching preview...' : 'Preview Import'}
                    </Button>
                    {previewTitle && <p className='text-sm text-muted-foreground'>Imported preview ready: {previewTitle}</p>}
                </div>
            </DialogContent>
        </Dialog>
    );
}
