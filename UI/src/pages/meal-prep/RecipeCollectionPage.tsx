import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ChevronsUpDown, Download, Minus, Plus, Share2, Trash2 } from 'lucide-react';
import { recipeCollectionsApi, recipesApi } from '@/lib/api';
import { RecipeCard } from '@/components/meal-prep/RecipeCard';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { Button } from '@/components/ui/button';
import {
    AlertDialog,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Progress } from '@/components/ui/progress';
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import type { RecipeCollectionExport } from '@/models/meal-prep';

function workspacePath(workspaceId: string, subPath: string) {
    const trimmed = subPath.replace(/^\//, '');
    return `/workspaces/${workspaceId}/${trimmed}`;
}

function slugifyDownloadName(name: string) {
    return name.replace(/[^\w\s-]/g, '').replace(/\s+/g, '-').slice(0, 80) || 'recipes';
}

async function writeExportToDirectory(
    targetDir: FileSystemDirectoryHandle,
    ownerWorkspaceId: string,
    data: RecipeCollectionExport,
) {
    const jsonHandle = await targetDir.getFileHandle('collection-export.json', { create: true });
    const jsonWriter = await jsonHandle.createWritable();
    await jsonWriter.write(JSON.stringify(data, null, 2));
    await jsonWriter.close();

    const imagesDir = await targetDir.getDirectoryHandle('images', { create: true });
    for (const recipe of data.recipes) {
        if (!recipe.imageFileName) continue;
        const response = await fetch(`/api/v1/workspaces/${ownerWorkspaceId}/recipes/${recipe.recipeId}/image`);
        if (!response.ok) continue;
        const blob = await response.blob();
        const imageHandle = await imagesDir.getFileHandle(recipe.imageFileName, { create: true });
        const imageWriter = await imageHandle.createWritable();
        await imageWriter.write(blob);
        await imageWriter.close();
    }
}

async function pickJsonFile(): Promise<File | null> {
    return await new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'application/json,.json';
        input.onchange = () => {
            const file = input.files?.[0] ?? null;
            resolve(file);
        };
        input.click();
    });
}

function dedupeExportRecipes(data: RecipeCollectionExport) {
    const seen = new Set<string>();
    const unique = data.recipes.filter(recipe => {
        const key = JSON.stringify(recipe.payload);
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
    });
    return {
        unique,
        duplicatesOmitted: data.recipes.length - unique.length,
    };
}

function toRecipeIdentityKey(title?: string | null, sourceUrl?: string | null) {
    const normalizedTitle = (title ?? '').trim().toLowerCase();
    const normalizedSourceUrl = (sourceUrl ?? '').trim().toLowerCase();
    return `${normalizedTitle}|${normalizedSourceUrl}`;
}

async function loadExistingRecipeIdentityKeys(workspaceId: string) {
    const existingKeys = new Set<string>();
    const pageSize = 100;
    let page = 1;
    let totalCount = 0;

    do {
        const response = await recipesApi.getAll(workspaceId, {
            page,
            pageSize,
            includeArchived: true,
        });
        totalCount = response.totalCount;
        for (const recipe of response.data) {
            existingKeys.add(toRecipeIdentityKey(recipe.title, recipe.sourceUrl ?? null));
        }
        page += 1;
    } while ((page - 1) * pageSize < totalCount);

    return existingKeys;
}

export default function RecipeCollectionPage() {
    const { workspaceId = '', collectionId = '' } = useParams<{
        workspaceId: string;
        collectionId: string;
    }>();
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { capture } = useAnalytics();
    const [deleteOpen, setDeleteOpen] = useState(false);
    const [recipePickerOpen, setRecipePickerOpen] = useState(false);
    const [shareDialogOpen, setShareDialogOpen] = useState(false);
    const [lastShareLink, setLastShareLink] = useState<string | null>(null);
    const [importState, setImportState] = useState({
        isActive: false,
        current: 0,
        total: 0,
        label: '',
    });

    const { data: detail, isLoading } = useQuery({
        queryKey: ['recipe-collection', workspaceId, collectionId],
        queryFn: () => recipeCollectionsApi.get(workspaceId, collectionId),
        enabled: Boolean(workspaceId && collectionId),
    });

    const ownerWorkspaceId = detail?.ownerWorkspaceId ?? workspaceId;
    const collectionRecipeIds = useMemo(() => new Set(detail?.recipes.map(recipe => recipe.id) ?? []), [detail?.recipes]);

    const { data: allRecipesPage, isLoading: isLoadingAllRecipes } = useQuery({
        queryKey: ['recipes', ownerWorkspaceId, '', 'collection-add'],
        queryFn: () =>
            recipesApi.getAll(ownerWorkspaceId, {
                page: 1,
                pageSize: 500,
                includeArchived: false,
            }),
        enabled: Boolean(ownerWorkspaceId) && Boolean(detail?.canEdit),
    });

    const availableRecipes = useMemo(
        () => (allRecipesPage?.data ?? []).filter(recipe => !collectionRecipeIds.has(recipe.id)),
        [allRecipesPage?.data, collectionRecipeIds],
    );

    const deleteCollection = useMutation({
        mutationFn: () => recipeCollectionsApi.remove(detail!.ownerWorkspaceId, collectionId),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            toast({ title: 'Collection deleted' });
            setDeleteOpen(false);
            navigate(workspacePath(workspaceId, '/'));
        },
    });

    const createShareLink = useMutation({
        mutationFn: () => recipeCollectionsApi.createShareLink(detail!.ownerWorkspaceId, collectionId),
        onSuccess: async data => {
            const absoluteUrl = `${window.location.origin}${data.importPath}`;
            setLastShareLink(absoluteUrl);
            try {
                if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
                    await navigator.clipboard.writeText(absoluteUrl);
                    toast({ title: 'Magic link copied' });
                    return;
                }
            } catch {
                // fall through to link-visible fallback toast
            }
            toast({
                title: 'Magic link generated',
                description: 'Clipboard is unavailable here. Copy the link from this dialog.',
            });
        },
    });

    async function handleExportDirectory() {
        if (!detail) return;
        try {
            const data = await recipeCollectionsApi.exportJson(workspaceId, collectionId);
            const picker = (window as Window & { showDirectoryPicker?: () => Promise<FileSystemDirectoryHandle> })
                .showDirectoryPicker;
            if (!picker) {
                const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `${slugifyDownloadName(data.collectionName)}-collection.json`;
                a.click();
                URL.revokeObjectURL(url);
                toast({ title: 'Directory export unavailable, downloaded JSON instead' });
                return;
            }
            const exportDir = await picker();
            await writeExportToDirectory(exportDir, ownerWorkspaceId, data);
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeCollectionExported,
                    withWorkspaceProperties(currentWorkspace, {
                        collection_id: collectionId,
                        recipe_count: data.recipes.length,
                    }),
                );
            }
            toast({ title: 'Export complete' });
        } catch {
            toast({ title: 'Export failed', variant: 'destructive' });
        }
    }

    async function handleImportDirectory() {
        if (!detail) return;
        try {
            async function importFromJsonPayload(data: RecipeCollectionExport) {
                const deduped = dedupeExportRecipes(data);
                const existingKeys = await loadExistingRecipeIdentityKeys(workspaceId);
                const candidates = deduped.unique.filter(
                    item => !existingKeys.has(toRecipeIdentityKey(item.payload.title, item.payload.sourceUrl ?? null)),
                );
                const skippedExisting = deduped.unique.length - candidates.length;
                const failedTitles: string[] = [];

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

                setImportState({
                    isActive: true,
                    current: 0,
                    total: candidates.length,
                    label: `Importing 0 of ${candidates.length} recipes`,
                });

                const created = await recipeCollectionsApi.create(workspaceId, {
                    name: `${data.collectionName} (Imported)`,
                    description: data.description ?? null,
                });

                for (const [index, item] of candidates.entries()) {
                    try {
                        const recipe = await recipesApi.create(workspaceId, item.payload);
                        await recipeCollectionsApi.addRecipe(workspaceId, created.id, recipe.id);
                        existingKeys.add(toRecipeIdentityKey(item.payload.title, item.payload.sourceUrl ?? null));
                    } catch {
                        failedTitles.push(item.title || item.payload.title || `Recipe ${index + 1}`);
                    }
                    const current = index + 1;
                    setImportState({
                        isActive: true,
                        current,
                        total: candidates.length,
                        label: `Importing ${current} of ${candidates.length} recipes`,
                    });
                }

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
                            ? `Failed and skipped ${failedTitles.length} item${failedTitles.length === 1 ? '' : 's'}: ${failedTitles.slice(0, 5).join(', ')}${failedTitles.length > 5 ? '…' : ''}`
                            : null,
                    ]
                        .filter(Boolean)
                        .join(' '),
                    variant: failedTitles.length > 0 ? 'destructive' : 'default',
                });
                setImportState({
                    isActive: false,
                    current: candidates.length,
                    total: candidates.length,
                    label: '',
                });
                navigate(workspacePath(workspaceId, `collections/${created.id}`));
            }

            const picker = (window as Window & { showDirectoryPicker?: () => Promise<FileSystemDirectoryHandle> })
                .showDirectoryPicker;
            if (!picker) {
                const jsonFile = await pickJsonFile();
                if (!jsonFile) return;
                const json = await jsonFile.text();
                const data = JSON.parse(json) as RecipeCollectionExport;
                await importFromJsonPayload(data);
                toast({ title: 'Directory import unavailable, imported from JSON instead' });
                return;
            }
            const importDir = await picker();
            const fileHandle = await importDir.getFileHandle('collection-export.json');
            const file = await fileHandle.getFile();
            const json = (await file.text()) as string;
            const data = JSON.parse(json) as RecipeCollectionExport;
            const deduped = dedupeExportRecipes(data);
            const existingKeys = await loadExistingRecipeIdentityKeys(workspaceId);
            const candidates = deduped.unique.filter(
                item => !existingKeys.has(toRecipeIdentityKey(item.payload.title, item.payload.sourceUrl ?? null)),
            );
            const skippedExisting = deduped.unique.length - candidates.length;
            const failedTitles: string[] = [];

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
                isActive: true,
                current: 0,
                total: candidates.length,
                label: `Importing 0 of ${candidates.length} recipes`,
            });

            let imagesDir: FileSystemDirectoryHandle | null = null;
            try {
                imagesDir = await importDir.getDirectoryHandle('images');
            } catch {
                imagesDir = null;
            }

            for (const [index, item] of candidates.entries()) {
                try {
                    const recipe = await recipesApi.create(workspaceId, item.payload);
                    await recipeCollectionsApi.addRecipe(workspaceId, created.id, recipe.id);
                    existingKeys.add(toRecipeIdentityKey(item.payload.title, item.payload.sourceUrl ?? null));
                    if (imagesDir && item.imageFileName) {
                        try {
                            const imageHandle = await imagesDir.getFileHandle(item.imageFileName);
                            const imageFile = await imageHandle.getFile();
                            await recipesApi.uploadImage(workspaceId, recipe.id, new File([imageFile], imageFile.name));
                        } catch {
                            // Continue importing even if one image is missing.
                        }
                    }
                } catch {
                    failedTitles.push(item.title || item.payload.title || `Recipe ${index + 1}`);
                }
                const current = index + 1;
                setImportState({
                    isActive: true,
                    current,
                    total: candidates.length,
                    label: `Importing ${current} of ${candidates.length} recipes`,
                });
            }

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
                        ? `Failed and skipped ${failedTitles.length} item${failedTitles.length === 1 ? '' : 's'}: ${failedTitles.slice(0, 5).join(', ')}${failedTitles.length > 5 ? '…' : ''}`
                        : null,
                ]
                    .filter(Boolean)
                    .join(' '),
                variant: failedTitles.length > 0 ? 'destructive' : 'default',
            });
            setImportState({
                isActive: false,
                current: candidates.length,
                total: candidates.length,
                label: '',
            });
            navigate(workspacePath(workspaceId, `collections/${created.id}`));
        } catch {
            setImportState({
                isActive: false,
                current: 0,
                total: 0,
                label: '',
            });
            toast({ title: 'Import failed', variant: 'destructive' });
        }
    }

    const removeRecipe = useMutation({
        mutationFn: (recipeId: string) =>
            recipeCollectionsApi.removeRecipe(detail!.ownerWorkspaceId, collectionId, recipeId),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collection', workspaceId, collectionId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            toast({ title: 'Removed from collection' });
        },
    });

    const addRecipe = useMutation({
        mutationFn: (recipeId: string) => recipeCollectionsApi.addRecipe(detail!.ownerWorkspaceId, collectionId, recipeId),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collection', workspaceId, collectionId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipes', ownerWorkspaceId, '', 'collection-add'] });
            toast({ title: 'Added to collection' });
        },
    });

    const addAllRecipes = useMutation({
        mutationFn: async () => {
            for (const recipe of availableRecipes) {
                await recipeCollectionsApi.addRecipe(detail!.ownerWorkspaceId, collectionId, recipe.id);
            }
        },
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['recipe-collection', workspaceId, collectionId] });
            void queryClient.invalidateQueries({ queryKey: ['recipe-collections', workspaceId] });
            void queryClient.invalidateQueries({ queryKey: ['recipes', ownerWorkspaceId, '', 'collection-add'] });
            toast({ title: `Added ${availableRecipes.length} recipes` });
        },
    });

    if (isLoading || !detail) {
        return (
            <div className='mx-auto max-w-6xl px-4 py-10 md:px-8'>
                <LoadingState label='Loading collection…' />
            </div>
        );
    }

    return (
        <div className='mx-auto max-w-6xl px-4 py-6 md:px-8 md:py-10'>
            <div className='mb-6'>
                <Link
                    to={workspacePath(workspaceId, '/')}
                    className='mb-4 inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground'
                >
                    <ArrowLeft className='h-4 w-4' />
                    Back to recipes
                </Link>
                <div className='flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between'>
                    <div>
                        <h1 className='font-heading text-3xl text-foreground md:text-4xl'>{detail.name}</h1>
                        {detail.description ? (
                            <p className='mt-2 text-muted-foreground'>{detail.description}</p>
                        ) : null}
                        {workspaceId !== detail.ownerWorkspaceId ? (
                            <p className='mt-2 text-sm text-muted-foreground'>
                                Shared into this workspace. Recipes open in their home workspace.
                            </p>
                        ) : null}
                    </div>
                    <div className='flex flex-wrap gap-2'>
                        <Button
                            type='button'
                            variant='outline'
                            size='sm'
                            className='gap-1.5'
                            onClick={() => void handleExportDirectory()}
                        >
                            <Download className='h-4 w-4' />
                            Export
                        </Button>
                        {detail.canEdit ? (
                            <Tooltip>
                                <TooltipTrigger asChild>
                                    <Button
                                        type='button'
                                        variant='outline'
                                        size='icon'
                                        aria-label='Share collection'
                                        onClick={() => setShareDialogOpen(true)}
                                    >
                                        <Share2 className='h-4 w-4' />
                                    </Button>
                                </TooltipTrigger>
                                <TooltipContent side='bottom'>Share with magic link</TooltipContent>
                            </Tooltip>
                        ) : null}
                        {detail.canEdit ? (
                            <>
                                <Button
                                    type='button'
                                    variant='outline'
                                    size='sm'
                                    className='gap-1.5 text-destructive hover:bg-destructive/10'
                                    onClick={() => setDeleteOpen(true)}
                                >
                                    <Trash2 className='h-4 w-4' />
                                    Delete
                                </Button>
                            </>
                        ) : null}
                    </div>
                </div>
            </div>

            {importState.isActive && (
                <div className='mb-6 rounded-lg border border-border bg-card p-4'>
                    <p className='mb-2 text-sm font-medium text-foreground'>Import in progress</p>
                    <p className='mb-3 text-xs text-muted-foreground'>{importState.label}</p>
                    <Progress
                        value={importState.total > 0 ? (importState.current / importState.total) * 100 : 0}
                        className='h-2'
                    />
                </div>
            )}

            {detail.canEdit && (
                <div className='mb-8 rounded-lg border border-border bg-card p-4'>
                    <div className='mb-3 flex items-center justify-between gap-3'>
                        <div>
                            <p className='text-sm font-medium text-foreground'>Add recipes</p>
                            <p className='text-xs text-muted-foreground'>
                                Search your library and add one recipe, or bulk add everything not yet in this collection.
                            </p>
                        </div>
                        <Button
                            type='button'
                            variant='outline'
                            size='sm'
                            disabled={availableRecipes.length === 0 || addAllRecipes.isPending}
                            onClick={() => void addAllRecipes.mutateAsync()}
                        >
                            Add all ({availableRecipes.length})
                        </Button>
                    </div>

                    <Popover open={recipePickerOpen} onOpenChange={setRecipePickerOpen}>
                        <PopoverTrigger asChild>
                            <Button
                                type='button'
                                variant='outline'
                                role='combobox'
                                aria-expanded={recipePickerOpen}
                                className='w-full justify-between'
                                disabled={isLoadingAllRecipes || availableRecipes.length === 0 || addRecipe.isPending}
                            >
                                <span className='truncate text-left'>
                                    {isLoadingAllRecipes
                                        ? 'Loading recipes...'
                                        : availableRecipes.length > 0
                                          ? 'Search recipes to add...'
                                          : 'All recipes are already in this collection'}
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
                                        {availableRecipes.map(recipe => (
                                            <CommandItem
                                                key={recipe.id}
                                                value={`${recipe.title} ${recipe.description ?? ''}`}
                                                onSelect={() => {
                                                    void addRecipe.mutateAsync(recipe.id);
                                                    setRecipePickerOpen(false);
                                                }}
                                            >
                                                <Plus className='mr-2 h-4 w-4 text-muted-foreground' />
                                                <span className='truncate'>{recipe.title}</span>
                                            </CommandItem>
                                        ))}
                                    </CommandGroup>
                                </CommandList>
                            </Command>
                        </PopoverContent>
                    </Popover>
                </div>
            )}

            {detail.recipes.length === 0 && (
                <EmptyState
                    title='No recipes in this collection'
                    description='Add recipes from the library or recipe page using “Add to collection”.'
                />
            )}

            {detail.recipes.length > 0 && (
                <div className='grid gap-6 sm:grid-cols-2 lg:grid-cols-3'>
                    {detail.recipes.map((recipe, index) => (
                        <div key={recipe.id} className='min-w-0'>
                            <RecipeCard
                                workspaceId={ownerWorkspaceId}
                                recipe={recipe}
                                index={index}
                                bottomRightAction={
                                    detail.canEdit ? (
                                        <Tooltip>
                                            <TooltipTrigger asChild>
                                                <Button
                                                    type='button'
                                                    variant='secondary'
                                                    size='icon'
                                                    className='h-8 w-8'
                                                    aria-label={`Remove ${recipe.title} from collection`}
                                                    onClick={e => {
                                                        e.preventDefault();
                                                        e.stopPropagation();
                                                        void removeRecipe.mutateAsync(recipe.id);
                                                    }}
                                                    disabled={removeRecipe.isPending}
                                                >
                                                    <Minus className='h-4 w-4' />
                                                </Button>
                                            </TooltipTrigger>
                                            <TooltipContent side='left'>Remove from collection</TooltipContent>
                                        </Tooltip>
                                    ) : null
                                }
                            />
                        </div>
                    ))}
                </div>
            )}

            <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Delete this collection?</AlertDialogTitle>
                        <AlertDialogDescription>
                            Recipes stay in your library; only the grouping is removed. Sharing links will stop working.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <Button
                            variant='destructive'
                            disabled={deleteCollection.isPending}
                            onClick={() => void deleteCollection.mutateAsync()}
                        >
                            Delete
                        </Button>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

            <Dialog open={shareDialogOpen} onOpenChange={setShareDialogOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Share with magic link</DialogTitle>
                        <DialogDescription>
                            Generate a link you can send to anyone. When opened, they can import this collection into a workspace of their choice.
                        </DialogDescription>
                    </DialogHeader>

                    {lastShareLink ? (
                        <div className='rounded-md border border-border bg-muted/30 p-3 text-sm text-muted-foreground break-all'>
                            {lastShareLink}
                        </div>
                    ) : null}

                    <DialogFooter>
                        <Button
                            type='button'
                            onClick={() => void createShareLink.mutateAsync()}
                            disabled={createShareLink.isPending}
                        >
                            {lastShareLink ? 'Generate new link and copy' : 'Generate and copy link'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
