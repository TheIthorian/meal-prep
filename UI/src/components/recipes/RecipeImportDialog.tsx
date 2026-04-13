import { type ReactNode, useState } from 'react';
import { recipesApi } from '@/lib/api';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { Recipe } from '@/models/meal-prep';

interface RecipeImportDialogProps {
    workspaceId: string;
    onImported: (recipe: Recipe) => void;
    trigger?: ReactNode;
}

export function RecipeImportDialog({ workspaceId, onImported, trigger }: RecipeImportDialogProps) {
    const [open, setOpen] = useState(false);
    const [url, setUrl] = useState('');
    const [isImporting, setImporting] = useState(false);

    const handleImport = async () => {
        setImporting(true);
        try {
            const recipe = await recipesApi.importFromUrl(workspaceId, url);
            onImported(recipe);
            setOpen(false);
            setUrl('');
        } finally {
            setImporting(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>{trigger ?? <Button variant='outline'>Import From URL</Button>}</DialogTrigger>
            <DialogContent className='sm:max-w-xl'>
                <DialogHeader>
                    <DialogTitle>Import Recipe From Web</DialogTitle>
                    <DialogDescription>
                        Paste a recipe URL. Meal Prep will import it directly into your library.
                    </DialogDescription>
                </DialogHeader>

                <div className='space-y-4'>
                    <Input
                        value={url}
                        onChange={event => setUrl(event.target.value)}
                        placeholder='https://example.com/recipe'
                    />
                    <Button onClick={handleImport} disabled={!url || isImporting}>
                        {isImporting ? 'Importing...' : 'Import Recipe'}
                    </Button>
                </div>
            </DialogContent>
        </Dialog>
    );
}
