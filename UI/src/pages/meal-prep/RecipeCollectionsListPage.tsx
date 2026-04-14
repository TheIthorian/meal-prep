import { Link, useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ChevronRight, FolderOpen, Plus, Upload } from 'lucide-react';
import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { recipeCollectionsApi, recipesApi } from '@/lib/api';
import { Button } from '@/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import type { RecipeCollectionExport } from '@/models/meal-prep';
import { Progress } from '@/components/ui/progress';

function workspacePath(workspaceId: string, subPath: string) {
    const trimmed = subPath.replace(/^\//, '');
    return `/workspaces/${workspaceId}/${trimmed}`;
}

async function pickJsonFile(): Promise<File | null> {
    return await new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'application/json,.json';
        input.onchange = () => resolve(input.files?.[0] ?? null);
        input.click();
    });
}

function toRecipeIdentityKey(title?: string | null, sourceUrl?: string | null) {
    return `${(title ?? '').trim().toLowerCase()}|${(sourceUrl ?? '').trim().toLowerCase()}`;
}

function dedupeExportRecipes(data: RecipeCollectionExport) {
    const seen = new Set<string>();
    const unique = data.recipes.filter(recipe => {
        const key = JSON.stringify(recipe.payload);
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
    });
    return { unique, duplicatesOmitted: data.recipes.length - unique.length };
}

async function loadExistingRecipeIdentityKeys(workspaceId: string) {
    const keys = new Set<string>();
    const pageSize = 100;
    let page = 1;
    let totalCount = 0;
    do {
        const response = await recipesApi.getAll(workspaceId, { page, pageSize, includeArchived: true });
        totalCount = response.totalCount;
        for (const recipe of response.data) keys.add(toRecipeIdentityKey(recipe.title, recipe.sourceUrl ?? null));
        page += 1;
    } while ((page - 1) * pageSize < totalCount);
    return keys;
}

export default function RecipeCollectionsListPage() {
    const { workspaceId = '' } = useParams<{ workspaceId: string }>();
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();
    const [createOpen, setCreateOpen] = useState(false);
    const [newName, setNewName] = useState('');
    const [importState, setImportState] = useState({
        isOpen: false,
        current: 0,
        total: 0,
        label: '',
    });
    const [importSummary, setImportSummary] = useState<{
        isOpen: boolean;
        createdCollectionId: string | null;
        importedCount: number;
        duplicatesOmitted: number;
        skippedExisting: number;
        failedTitles: string[];
    }>({
        isOpen: false,
        createdCollectionId: null,
        importedCount: 0,
        duplicatesOmitted: 0,
        skippedExisting: 0,
        failedTitles: [],
    });

    const { data: collections = [], isLoading } = useQuery({
        queryKey: ['recipe-collections', workspaceId],
        queryFn: () => recipeCollectionsApi.list(workspaceId),
        enabled: Boolean(workspaceId),
    });

    const createCollection = useMutation({
        mutationFn: (name: string) => recipeCollectionsApi.create(workspaceId, { name }),
        onSuccess: data => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            setCreateOpen(false);
            setNewName('');
            toast({ title: 'Collection created' });
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeCollectionCreated,
                    withWorkspaceProperties(currentWorkspace, { collection_id: data.id }),
                );
            }
        },
    });

    function handleCreateSubmit(e: React.FormEvent) {
        e.preventDefault();
        const name = newName.trim();
        if (!name) return;
        void createCollection.mutateAsync(name);
    }

    async function handleImport() {
        try {
            const picker = (window as Window & { showDirectoryPicker?: () => Promise<FileSystemDirectoryHandle> })
                .showDirectoryPicker;

            let data: RecipeCollectionExport;
            let imagesDir: FileSystemDirectoryHandle | null = null;

            if (picker) {
                const importDir = await picker();
                const fileHandle = await importDir.getFileHandle('collection-export.json');
                const file = await fileHandle.getFile();
                data = JSON.parse(await file.text()) as RecipeCollectionExport;
                try {
                    imagesDir = await importDir.getDirectoryHandle('images');
                } catch {
                    imagesDir = null;
                }
            } else {
                const jsonFile = await pickJsonFile();
                if (!jsonFile) return;
                data = JSON.parse(await jsonFile.text()) as RecipeCollectionExport;
                toast({ title: 'Directory import unavailable, imported from JSON instead' });
            }

            const deduped = dedupeExportRecipes(data);
            const existingKeys = await loadExistingRecipeIdentityKeys(workspaceId);
            const candidates = deduped.unique.filter(
                item => !existingKeys.has(toRecipeIdentityKey(item.payload.title, item.payload.sourceUrl ?? null)),
            );
            const skippedExisting = deduped.unique.length - candidates.length;
            const failedTitles: string[] = [];
            let importedCount = 0;

            if (candidates.length === 0) {
                toast({
                    title: 'Nothing to import',
                    description:
                        skippedExisting > 0
                            ? `All ${skippedExisting} recipe${skippedExisting === 1 ? '' : 's'} already exist in this workspace.`
                            : 'No importable recipes found.',
                });
                return;
            }

            const created = await recipeCollectionsApi.create(workspaceId, {
                name: `${data.collectionName} (Imported)`,
                description: data.description ?? null,
            });

            setImportState({
                isOpen: true,
                current: 0,
                total: candidates.length,
                label: `Importing 0 of ${candidates.length} recipes`,
            });

            for (const [index, item] of candidates.entries()) {
                try {
                    const recipe = await recipesApi.create(workspaceId, item.payload);
                    await recipeCollectionsApi.addRecipe(workspaceId, created.id, recipe.id);
                    existingKeys.add(toRecipeIdentityKey(item.payload.title, item.payload.sourceUrl ?? null));
                    importedCount += 1;
                    if (imagesDir && item.imageFileName) {
                        try {
                            const imageHandle = await imagesDir.getFileHandle(item.imageFileName);
                            const imageFile = await imageHandle.getFile();
                            await recipesApi.uploadImage(workspaceId, recipe.id, new File([imageFile], imageFile.name));
                        } catch {
                            // non-blocking image error
                        }
                    }
                } catch {
                    failedTitles.push(item.title || item.payload.title || `Recipe ${index + 1}`);
                }

                const current = index + 1;
                setImportState({
                    isOpen: true,
                    current,
                    total: candidates.length,
                    label: `Importing ${current} of ${candidates.length} recipes`,
                });
            }

            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            toast({
                title: failedTitles.length > 0 ? 'Import complete with skipped items' : 'Import complete',
                description: [
                    deduped.duplicatesOmitted > 0
                        ? `Omitted ${deduped.duplicatesOmitted} duplicate recipe${deduped.duplicatesOmitted === 1 ? '' : 's'}.`
                        : null,
                    skippedExisting > 0
                        ? `Skipped ${skippedExisting} existing recipe${skippedExisting === 1 ? '' : 's'}.`
                        : null,
                    failedTitles.length > 0
                        ? `Failed and skipped ${failedTitles.length} item${failedTitles.length === 1 ? '' : 's'}.`
                        : null,
                ]
                    .filter(Boolean)
                    .join(' '),
                variant: failedTitles.length > 0 ? 'destructive' : 'default',
            });

            setImportState({
                isOpen: false,
                current: candidates.length,
                total: candidates.length,
                label: '',
            });
            setImportSummary({
                isOpen: true,
                createdCollectionId: created.id,
                importedCount,
                duplicatesOmitted: deduped.duplicatesOmitted,
                skippedExisting,
                failedTitles,
            });
        } catch {
            setImportState({
                isOpen: false,
                current: 0,
                total: 0,
                label: '',
            });
            toast({ title: 'Import failed', variant: 'destructive' });
        }
    }

    return (
        <div className='mx-auto max-w-lg px-4 py-6 md:px-8 md:py-10'>
            <div className='mb-6 flex items-start justify-between gap-4'>
                <div>
                    <h1 className='font-heading text-2xl text-foreground md:text-3xl'>Collections</h1>
                    <p className='mt-1 text-sm text-muted-foreground'>
                        Group recipes, share with another workspace, or export a bundle.
                    </p>
                </div>
                <Dialog open={createOpen} onOpenChange={setCreateOpen}>
                    <DialogTrigger asChild>
                        <Button type='button' size='sm' className='shrink-0 gap-1'>
                            <Plus className='h-4 w-4' />
                            New
                        </Button>
                    </DialogTrigger>
                    <DialogContent>
                        <form onSubmit={handleCreateSubmit}>
                            <DialogHeader>
                                <DialogTitle>New collection</DialogTitle>
                            </DialogHeader>
                            <div className='py-4'>
                                <Label htmlFor='m-collection-name'>Name</Label>
                                <Input
                                    id='m-collection-name'
                                    value={newName}
                                    onChange={e => setNewName(e.target.value)}
                                    placeholder='e.g. Weeknight dinners'
                                    className='mt-2'
                                    autoFocus
                                />
                            </div>
                            <DialogFooter>
                                <Button type='submit' disabled={!newName.trim() || createCollection.isPending}>
                                    Create
                                </Button>
                            </DialogFooter>
                        </form>
                    </DialogContent>
                </Dialog>
                <Button type='button' size='sm' variant='outline' className='shrink-0 gap-1' onClick={() => void handleImport()}>
                    <Upload className='h-4 w-4' />
                    Import
                </Button>
            </div>

            <Link
                to={workspacePath(workspaceId, '/')}
                className='mb-4 flex items-center gap-2 rounded-lg border border-border bg-card px-4 py-3 text-sm font-medium text-foreground transition-colors hover:bg-secondary'
            >
                <FolderOpen className='h-4 w-4 text-primary' />
                <span className='flex-1'>All recipes</span>
                <ChevronRight className='h-4 w-4 text-muted-foreground' />
            </Link>

            {isLoading && <LoadingState label='Loading collections…' />}

            {!isLoading && collections.length === 0 && (
                <EmptyState
                    title='No collections yet'
                    description='Create a collection from the button above, then add recipes from the library or a recipe page.'
                />
            )}

            {!isLoading && collections.length > 0 && (
                <ul className='space-y-2'>
                    {collections.map(collection => (
                        <li key={collection.id}>
                            <Link
                                to={workspacePath(workspaceId, `collections/${collection.id}`)}
                                className='flex items-center gap-3 rounded-lg border border-border bg-card px-4 py-3 text-sm transition-colors hover:bg-secondary'
                            >
                                <span className='min-w-0 flex-1 truncate font-medium text-foreground'>
                                    {collection.name}
                                </span>
                                {!collection.isOwnedByViewerWorkspace ? (
                                    <span className='shrink-0 text-[10px] font-semibold uppercase text-muted-foreground'>
                                        Shared
                                    </span>
                                ) : null}
                                <span className='shrink-0 text-xs text-muted-foreground'>{collection.recipeCount}</span>
                                <ChevronRight className='h-4 w-4 shrink-0 text-muted-foreground' />
                            </Link>
                        </li>
                    ))}
                </ul>
            )}

            <Dialog open={importState.isOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Importing collection</DialogTitle>
                        <DialogDescription>
                            {importState.label || 'Preparing import...'}
                        </DialogDescription>
                    </DialogHeader>
                    <Progress
                        value={importState.total > 0 ? (importState.current / importState.total) * 100 : 0}
                        className='h-2'
                    />
                </DialogContent>
            </Dialog>

            <Dialog
                open={importSummary.isOpen}
                onOpenChange={open => setImportSummary(previous => ({ ...previous, isOpen: open }))}
            >
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Import summary</DialogTitle>
                        <DialogDescription>
                            Imported {importSummary.importedCount} recipe
                            {importSummary.importedCount === 1 ? '' : 's'}.
                        </DialogDescription>
                    </DialogHeader>

                    <div className='space-y-2 text-sm text-muted-foreground'>
                        {importSummary.duplicatesOmitted > 0 ? (
                            <p>
                                Omitted {importSummary.duplicatesOmitted} duplicate recipe
                                {importSummary.duplicatesOmitted === 1 ? '' : 's'} from the import file.
                            </p>
                        ) : null}
                        {importSummary.skippedExisting > 0 ? (
                            <p>
                                Skipped {importSummary.skippedExisting} existing recipe
                                {importSummary.skippedExisting === 1 ? '' : 's'} already in this workspace.
                            </p>
                        ) : null}
                        {importSummary.failedTitles.length > 0 ? (
                            <div>
                                <p className='font-medium text-foreground'>
                                    Failed and skipped {importSummary.failedTitles.length} recipe
                                    {importSummary.failedTitles.length === 1 ? '' : 's'}:
                                </p>
                                <ul className='mt-1 max-h-40 list-disc space-y-1 overflow-y-auto pl-5'>
                                    {importSummary.failedTitles.map(title => (
                                        <li key={title}>{title}</li>
                                    ))}
                                </ul>
                            </div>
                        ) : null}
                    </div>

                    <DialogFooter>
                        {importSummary.createdCollectionId ? (
                            <Button
                                type='button'
                                onClick={() => {
                                    const id = importSummary.createdCollectionId;
                                    setImportSummary(previous => ({ ...previous, isOpen: false }));
                                    navigate(workspacePath(workspaceId, `collections/${id}`));
                                }}
                            >
                                Open collection
                            </Button>
                        ) : null}
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
