import { type ReactNode, useState } from 'react';
import { FileUp, Link2 } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
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
            <DialogTrigger asChild>{trigger ?? <Button variant='outline'>Add recipe</Button>}</DialogTrigger>
            <DialogContent className='sm:max-w-xl'>
                <DialogHeader>
                    <DialogTitle>Add a recipe</DialogTitle>
                    <DialogDescription>
                        Give Meal Prep a recipe and it writes the ingredients and method into your library for you.
                    </DialogDescription>
                </DialogHeader>

                <div className='space-y-3'>
                    <div>
                        <Label htmlFor='recipe-import-url' className='flex items-center gap-2'>
                            <Link2 className='h-4 w-4 text-muted-foreground' aria-hidden />
                            From a website
                        </Label>
                        <p className='mt-1 text-sm text-muted-foreground'>
                            Paste a link to a recipe page: a blog, a newspaper, a supermarket site.
                        </p>
                    </div>
                    <Input
                        id='recipe-import-url'
                        value={url}
                        onChange={event => setUrl(event.target.value)}
                        placeholder='https://example.com/recipe'
                    />
                    <Button onClick={handleImportFromUrl} disabled={!url || isImportingFromUrl || isImportingFromFile}>
                        {isImportingFromUrl ? 'Reading link...' : 'Add from link'}
                    </Button>
                </div>

                <div className='space-y-3 border-t border-border pt-4'>
                    <div>
                        <Label htmlFor='recipe-import-file' className='flex items-center gap-2'>
                            <FileUp className='h-4 w-4 text-muted-foreground' aria-hidden />
                            From a photo or file
                        </Label>
                        <p className='mt-1 text-sm text-muted-foreground'>
                            Take a photo of a cookbook page or a handwritten card, or upload a screenshot, PDF or text
                            file. The text is read for you.
                        </p>
                    </div>
                    <Input
                        id='recipe-import-file'
                        type='file'
                        accept='.pdf,.txt,image/png,image/jpeg,image/jpg,image/webp'
                        onChange={event => setSelectedFile(event.target.files?.[0] ?? null)}
                    />
                    <Button
                        onClick={handleImportFromFile}
                        disabled={!selectedFile || isImportingFromUrl || isImportingFromFile}
                    >
                        {isImportingFromFile ? 'Reading file...' : 'Add from photo or file'}
                    </Button>
                </div>
            </DialogContent>
        </Dialog>
    );
}
