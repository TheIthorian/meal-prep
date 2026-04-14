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
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [isImportingFromUrl, setImportingFromUrl] = useState(false);
    const [isImportingFromFile, setImportingFromFile] = useState(false);

    const handleImportFromUrl = async () => {
        setImportingFromUrl(true);
        try {
            const recipe = await recipesApi.importFromUrl(workspaceId, url);
            onImported(recipe);
            setOpen(false);
            setUrl('');
        } finally {
            setImportingFromUrl(false);
        }
    };

    const handleImportFromFile = async () => {
        if (!selectedFile) return;

        setImportingFromFile(true);
        try {
            const recipe = await recipesApi.importFromFile(workspaceId, selectedFile);
            onImported(recipe);
            setOpen(false);
            setSelectedFile(null);
        } finally {
            setImportingFromFile(false);
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
                    <Button onClick={handleImportFromUrl} disabled={!url || isImportingFromUrl || isImportingFromFile}>
                        {isImportingFromUrl ? 'Importing URL...' : 'Import From URL'}
                    </Button>
                </div>

                <div className='space-y-4'>
                    <Input
                        type='file'
                        accept='.pdf,.txt,image/png,image/jpeg,image/jpg,image/webp'
                        onChange={event => setSelectedFile(event.target.files?.[0] ?? null)}
                    />
                    <Button
                        onClick={handleImportFromFile}
                        disabled={!selectedFile || isImportingFromUrl || isImportingFromFile}
                    >
                        {isImportingFromFile ? 'Importing File...' : 'Import From File'}
                    </Button>
                </div>
            </DialogContent>
        </Dialog>
    );
}
