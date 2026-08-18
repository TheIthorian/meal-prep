import { useRef, useState, type DragEvent, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { FileArchive, Upload, X } from 'lucide-react';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { MAX_IMPORT_RECIPES } from '@/lib/collection-transfer';

const ACCEPTED_EXTENSIONS = ['.zip', '.json'];

function isBundleFile(file: File) {
    return ACCEPTED_EXTENSIONS.some(extension => file.name.toLowerCase().endsWith(extension));
}

function formatFileSize(bytes: number) {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

interface ImportCollectionBundleDialogProps {
    /** Called with the chosen bundle once the reader confirms. The dialog closes first. */
    onImport: (file: File) => void;
    /** Route of the recipe library, for the pointer to the everyday way of adding a recipe. */
    recipesTo: string;
    trigger: ReactNode;
}

/**
 * Bundle import is the rare counterpart to Export, so the dialog carries the explanation the
 * button label cannot: what a bundle is, where one comes from, and what importing does. The drop
 * zone takes the file directly — most people arrive here with the .zip already in a Finder window.
 */
export function ImportCollectionBundleDialog({ onImport, recipesTo, trigger }: ImportCollectionBundleDialogProps) {
    const [open, setOpen] = useState(false);
    const [file, setFile] = useState<File | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [isDraggingOver, setDraggingOver] = useState(false);
    const inputRef = useRef<HTMLInputElement>(null);

    function chooseFile(candidate: File | null | undefined) {
        if (!candidate) return;
        if (!isBundleFile(candidate)) {
            setFile(null);
            setError(`${candidate.name} is not a bundle. Pick the .zip or .json file that Export produced.`);
            return;
        }
        setError(null);
        setFile(candidate);
    }

    function handleDrop(event: DragEvent<HTMLDivElement>) {
        event.preventDefault();
        setDraggingOver(false);
        chooseFile(event.dataTransfer.files?.[0]);
    }

    function handleOpenChange(next: boolean) {
        setOpen(next);
        if (!next) {
            setFile(null);
            setError(null);
            setDraggingOver(false);
        }
    }

    return (
        <Dialog open={open} onOpenChange={handleOpenChange}>
            <DialogTrigger asChild>{trigger}</DialogTrigger>
            {/* The shared dialog is a grid with an auto column, so a long file name would widen it
                past a phone screen; the explicit minmax column keeps it inside the viewport. */}
            <DialogContent className='w-[calc(100%-2rem)] grid-cols-[minmax(0,1fr)] rounded-2xl sm:w-full sm:max-w-lg sm:rounded-2xl'>
                <DialogHeader>
                    <DialogTitle>Import a collection bundle</DialogTitle>
                    <DialogDescription>
                        A bundle is the .zip file containing all recipes in collection.<br/>
                        Use this to import a bundle you received from someone else, or to move collections between workspaces.
                    </DialogDescription>
                </DialogHeader>

                <div
                    onDragOver={event => {
                        event.preventDefault();
                        setDraggingOver(true);
                    }}
                    onDragLeave={() => setDraggingOver(false)}
                    onDrop={handleDrop}
                    // The zone keeps its height whether or not a file is chosen, so picking one
                    // does not make the dialog jump under the pointer.
                    className={`flex min-h-[11rem] flex-col justify-center rounded-xl border border-dashed p-6 text-center transition-colors ${
                        isDraggingOver ? 'border-primary bg-primary/10' : 'border-border bg-secondary/40'
                    }`}
                >
                    {file ? (
                        <div className='flex items-center gap-3 rounded-lg border border-border bg-card p-3 text-left'>
                            <FileArchive className='h-8 w-8 shrink-0 text-primary' aria-hidden />
                            <span className='min-w-0 flex-1'>
                                <span className='block truncate text-sm font-medium text-foreground'>{file.name}</span>
                                <span className='block text-xs text-muted-foreground'>{formatFileSize(file.size)}</span>
                            </span>
                            <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='shrink-0 gap-1'
                                aria-label='Remove the chosen bundle'
                                onClick={() => setFile(null)}
                            >
                                <X className='h-4 w-4' aria-hidden />
                                {/* The file name gets the room on a phone; the icon still says remove. */}
                                <span className='hidden sm:inline'>Remove</span>
                            </Button>
                        </div>
                    ) : (
                        <>
                            <Upload className='mx-auto h-8 w-8 text-muted-foreground' aria-hidden />
                            {/* Dropping is a pointer gesture; on a phone the button is the whole story. */}
                            <p className='mt-3 text-sm font-medium text-foreground'>
                                <span className='hidden sm:inline'>Drop a bundle here</span>
                                <span className='sm:hidden'>Choose a bundle</span>
                            </p>
                            <p className='mt-1 text-xs text-muted-foreground'>
                                .zip or .json, up to {MAX_IMPORT_RECIPES.toLocaleString()} recipes
                            </p>
                            <Button
                                type='button'
                                variant='outline'
                                size='sm'
                                className='mt-4 self-center'
                                onClick={() => inputRef.current?.click()}
                            >
                                Choose a file
                            </Button>
                        </>
                    )}

                    <input
                        ref={inputRef}
                        type='file'
                        accept='.zip,.json,application/zip,application/json'
                        className='sr-only'
                        aria-label='Collection bundle file'
                        onChange={event => chooseFile(event.target.files?.[0])}
                    />
                </div>

                {error && <p className='text-sm text-destructive'>{error}</p>}

                <DialogFooter className='sm:items-center sm:justify-between'>
                    <p className='text-xs text-muted-foreground sm:mr-auto'>
                        Adding a single recipe from a link or a photo? Use{' '}
                        <Link
                            to={recipesTo}
                            className='font-medium text-primary hover:underline'
                            onClick={() => handleOpenChange(false)}
                        >
                            Add recipe
                        </Link>{' '}
                        on the Recipes page.
                    </p>
                    <Button
                        type='button'
                        disabled={!file}
                        onClick={() => {
                            if (!file) return;
                            handleOpenChange(false);
                            onImport(file);
                        }}
                    >
                        Import bundle
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
