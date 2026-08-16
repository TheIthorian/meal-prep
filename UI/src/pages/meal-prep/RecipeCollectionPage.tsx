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
import { toast } from '@/hooks/use-toast';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { buildCollectionZip, downloadBlob, slugifyDownloadName } from '@/lib/collection-transfer';
import { recipeImageRequestUrl } from '@/lib/meal-prep';
import type { RecipeCollectionExport, RecipeListItem } from '@/models/meal-prep';

function workspacePath(workspaceId: string, subPath: string) {
    const trimmed = subPath.replace(/^\//, '');
    return `/workspaces/${workspaceId}/${trimmed}`;
}

/** Fetches the cover image bytes for every recipe in the export that has one. */
async function loadExportImages(ownerWorkspaceId: string, data: RecipeCollectionExport) {
    const images = new Map<string, Uint8Array>();
    for (const recipe of data.recipes) {
        if (!recipe.imageFileName) continue;
        try {
            const response = await fetch(recipeImageRequestUrl(ownerWorkspaceId, recipe.recipeId), {
                credentials: 'include',
            });
            if (!response.ok) continue;
            images.set(recipe.imageFileName, new Uint8Array(await response.arrayBuffer()));
        } catch {
            // A missing image must not fail the whole export.
        }
    }
    return images;
}

async function loadAllRecipes(workspaceId: string, options: { includeArchived: boolean }) {
    const recipes: RecipeListItem[] = [];
    const pageSize = 100;
    let page = 1;
    let totalCount = 0;

    do {
        const response = await recipesApi.getAll(workspaceId, {
            page,
            pageSize,
            includeArchived: options.includeArchived,
        });
        totalCount = response.totalCount;
        recipes.push(...response.data);
        page += 1;
    } while ((page - 1) * pageSize < totalCount);

    return recipes;
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
    const [isExporting, setIsExporting] = useState(false);

    const { data: detail, isLoading } = useQuery({
        queryKey: ['recipe-collection', workspaceId, collectionId],
        queryFn: () => recipeCollectionsApi.get(workspaceId, collectionId),
        enabled: Boolean(workspaceId && collectionId),
    });

    const ownerWorkspaceId = detail?.ownerWorkspaceId ?? workspaceId;
    const collectionRecipeIds = useMemo(() => new Set(detail?.recipes.map(recipe => recipe.id) ?? []), [detail?.recipes]);

    const { data: allRecipes, isLoading: isLoadingAllRecipes } = useQuery({
        queryKey: ['recipes', ownerWorkspaceId, '', 'collection-add'],
        queryFn: () => loadAllRecipes(ownerWorkspaceId, { includeArchived: false }),
        enabled: Boolean(ownerWorkspaceId) && Boolean(detail?.canEdit),
    });

    const availableRecipes = useMemo(
        () => (allRecipes ?? []).filter(recipe => !collectionRecipeIds.has(recipe.id)),
        [allRecipes, collectionRecipeIds],
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

    async function handleExport() {
        if (!detail) return;
        setIsExporting(true);
        try {
            const data = await recipeCollectionsApi.exportJson(workspaceId, collectionId);
            const images = await loadExportImages(ownerWorkspaceId, data);
            const zip = buildCollectionZip(data, images);
            downloadBlob(zip, `${slugifyDownloadName(data.collectionName)}-collection.zip`);
            if (currentWorkspace) {
                capture(
                    analyticsEvents.recipeCollectionExported,
                    withWorkspaceProperties(currentWorkspace, {
                        collection_id: collectionId,
                        recipe_count: data.recipes.length,
                    }),
                );
            }
            toast({
                title: 'Export downloaded',
                description: `${data.recipes.length} recipe${data.recipes.length === 1 ? '' : 's'} and ${images.size} image${images.size === 1 ? '' : 's'}. Import it from the Collections page.`,
            });
        } catch {
            toast({ title: 'Export failed', variant: 'destructive' });
        } finally {
            setIsExporting(false);
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
                            disabled={isExporting}
                            onClick={() => void handleExport()}
                        >
                            <Download className='h-4 w-4' />
                            {isExporting ? 'Exporting…' : 'Export'}
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
