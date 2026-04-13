import { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Sparkles } from 'lucide-react';
import { recipesApi } from '@/lib/api';
import { formatRecipeTagLabel } from '@/lib/meal-prep';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

interface RecipeTagsFieldProps {
    workspaceId: string;
    selectedTags: string[];
    onChange: (tags: string[]) => void;
    title: string;
    description: string | null | undefined;
    ingredientNames: string[];
    stepInstructions: string[];
}

export function RecipeTagsField({
    workspaceId,
    selectedTags,
    onChange,
    title,
    description,
    ingredientNames,
    stepInstructions,
}: RecipeTagsFieldProps) {
    const [filter, setFilter] = useState('');

    const { data: whitelist } = useQuery({
        queryKey: ['recipe-tags', workspaceId],
        queryFn: () => recipesApi.getTagWhitelist(workspaceId),
        enabled: Boolean(workspaceId),
    });

    const suggestMutation = useMutation({
        mutationFn: () =>
            recipesApi.suggestTags(workspaceId, {
                title,
                description: description ?? null,
                ingredientNames,
                stepInstructions,
            }),
    });

    const selectedSet = useMemo(() => new Set(selectedTags), [selectedTags]);

    const filteredWhitelist = useMemo(() => {
        const list = whitelist?.tags ?? [];
        const q = filter.trim().toLowerCase();
        if (!q) return list;
        return list.filter(tag => tag.toLowerCase().includes(q) || formatRecipeTagLabel(tag).toLowerCase().includes(q));
    }, [whitelist?.tags, filter]);

    function toggleTag(tag: string) {
        const next = new Set(selectedTags);
        if (next.has(tag)) next.delete(tag);
        else next.add(tag);
        onChange([...next].sort((a, b) => a.localeCompare(b)));
    }

    async function handleSuggest() {
        const result = await suggestMutation.mutateAsync();
        const merged = new Set([...selectedTags, ...result.tags]);
        onChange([...merged].sort((a, b) => a.localeCompare(b)));
    }

    return (
        <div className='space-y-3'>
            <div className='flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between'>
                <div className='space-y-2 sm:flex-1'>
                    <label className='text-sm font-medium'>Tags</label>
                    <Input
                        value={filter}
                        onChange={e => setFilter(e.target.value)}
                        placeholder='Search tags…'
                        className='max-w-md'
                    />
                </div>
                <Button
                    type='button'
                    variant='secondary'
                    className='shrink-0 gap-2'
                    disabled={!title.trim() || suggestMutation.isPending}
                    onClick={() => void handleSuggest()}
                >
                    <Sparkles className='h-4 w-4' />
                    {suggestMutation.isPending ? 'Suggesting…' : 'Suggest tags'}
                </Button>
            </div>

            {selectedTags.length > 0 && (
                <div className='flex flex-wrap gap-2'>
                    {selectedTags.map(tag => (
                        <button
                            key={tag}
                            type='button'
                            onClick={() => toggleTag(tag)}
                            className='rounded-full bg-primary px-3 py-1 text-xs font-medium text-primary-foreground hover:opacity-90'
                            title='Click to remove'
                        >
                            {formatRecipeTagLabel(tag)} ×
                        </button>
                    ))}
                </div>
            )}

            <div className='max-h-48 overflow-y-auto rounded-lg border border-border bg-card/50 p-3'>
                <div className='flex flex-wrap gap-2'>
                    {filteredWhitelist.map(tag => (
                        <button
                            key={tag}
                            type='button'
                            onClick={() => toggleTag(tag)}
                            className={cn(
                                'rounded-full px-3 py-1 text-xs font-medium transition-colors',
                                selectedSet.has(tag)
                                    ? 'bg-primary/15 text-primary ring-1 ring-primary/40'
                                    : 'bg-secondary text-secondary-foreground hover:bg-secondary/80',
                            )}
                        >
                            {formatRecipeTagLabel(tag)}
                        </button>
                    ))}
                </div>
                {filteredWhitelist.length === 0 && (
                    <p className='text-sm text-muted-foreground'>No tags match your search.</p>
                )}
            </div>
        </div>
    );
}
