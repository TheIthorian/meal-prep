import { useEffect, useRef, useState } from 'react';
import { ImageIcon, Loader2, Trash2, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { recipesApi } from '@/lib/api';
import { RecipeCoverImage } from '@/components/meal-prep/RecipeCoverImage';
import { cn } from '@/lib/utils';

interface RecipePhotoSectionProps {
    workspaceId: string;
    recipeId: string;
    hasImage: boolean;
    title: string;
    onImageChanged: (nextHasImage: boolean) => void;
}

const ACCEPT = 'image/jpeg,image/png,image/webp,image/gif';

export function RecipePhotoSection({ workspaceId, recipeId, hasImage, title, onImageChanged }: RecipePhotoSectionProps) {
    const inputRef = useRef<HTMLInputElement>(null);
    const [busy, setBusy] = useState(false);
    const [imageRevision, setImageRevision] = useState(0);

    useEffect(() => {
        setImageRevision(0);
    }, [recipeId]);

    async function applyFile(file: File | null | undefined) {
        if (!file || !file.type.startsWith('image/')) return;
        setBusy(true);
        try {
            await recipesApi.uploadImage(workspaceId, recipeId, file);
            setImageRevision(r => r + 1);
            onImageChanged(true);
        } finally {
            setBusy(false);
        }
    }

    function onFileChange(e: React.ChangeEvent<HTMLInputElement>) {
        const file = e.target.files?.[0];
        void applyFile(file);
        e.target.value = '';
    }

    function onDrop(e: React.DragEvent) {
        e.preventDefault();
        e.stopPropagation();
        const file = e.dataTransfer.files?.[0];
        void applyFile(file);
    }

    function onDragOver(e: React.DragEvent) {
        e.preventDefault();
        e.stopPropagation();
    }

    async function onRemove() {
        setBusy(true);
        try {
            await recipesApi.deleteImage(workspaceId, recipeId);
            setImageRevision(r => r + 1);
            onImageChanged(false);
        } finally {
            setBusy(false);
        }
    }

    function onPaste(e: React.ClipboardEvent) {
        const items = e.clipboardData?.items;
        if (!items?.length) return;
        for (let i = 0; i < items.length; i++) {
            const item = items[i];
            if (item.kind === 'file' && item.type.startsWith('image/')) {
                const file = item.getAsFile();
                if (file) void applyFile(file);
                e.preventDefault();
                return;
            }
        }
    }

    return (
        <div className='mb-8'>
            <h2 className='font-heading mb-3 text-lg text-foreground'>Photo</h2>
            <div
                role='group'
                aria-label='Recipe photo'
                tabIndex={0}
                onPaste={onPaste}
                onDrop={onDrop}
                onDragOver={onDragOver}
                className={cn(
                    'relative overflow-hidden rounded-xl border border-border/50 bg-muted/30 outline-none transition-colors focus-visible:ring-2 focus-visible:ring-primary/30',
                    busy && 'pointer-events-none opacity-70',
                )}
            >
                <div className='aspect-[16/9] w-full'>
                    {hasImage ? (
                        <RecipeCoverImage
                            key={imageRevision}
                            workspaceId={workspaceId}
                            recipeId={recipeId}
                            hasImage={hasImage}
                            alt={`Photo of ${title}`}
                            className='h-full w-full object-cover'
                        />
                    ) : (
                        <div className='flex h-full w-full flex-col items-center justify-center gap-2 px-4 text-center text-muted-foreground'>
                            <ImageIcon className='h-10 w-10 opacity-40' />
                            <p className='text-sm'>No photo yet</p>
                        </div>
                    )}
                </div>

                <div className='flex flex-wrap items-center gap-2 border-t border-border/50 bg-card/80 px-3 py-2.5 backdrop-blur-sm'>
                    <input
                        ref={inputRef}
                        type='file'
                        accept={ACCEPT}
                        className='sr-only'
                        onChange={onFileChange}
                        disabled={busy}
                    />
                    <Button
                        type='button'
                        variant='secondary'
                        size='sm'
                        disabled={busy}
                        onClick={() => inputRef.current?.click()}
                        className='gap-1.5'
                    >
                        {busy ? <Loader2 className='h-4 w-4 animate-spin' /> : <Upload className='h-4 w-4' />}
                        Upload
                    </Button>
                    {hasImage ? (
                        <Button
                            type='button'
                            variant='outline'
                            size='sm'
                            disabled={busy}
                            onClick={() => void onRemove()}
                            className='gap-1.5 text-destructive hover:text-destructive'
                        >
                            <Trash2 className='h-4 w-4' />
                            Remove
                        </Button>
                    ) : null}
                    <p className='ml-auto max-w-[min(100%,18rem)] text-right text-xs text-muted-foreground'>
                        Drop a file here, or click Upload. Click this area, then paste (⌘V / Ctrl+V) to add from the clipboard.
                    </p>
                </div>
            </div>
        </div>
    );
}
