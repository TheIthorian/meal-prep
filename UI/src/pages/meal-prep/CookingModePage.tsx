import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowLeft, Carrot, ExternalLink } from 'lucide-react';
import { recipesApi } from '@/lib/api';
import { splitInstructionIntoSentences } from '@/lib/instruction-sentences';
import { safeHttpUrlHref, scaleRecipeIngredients } from '@/lib/meal-prep';
import { cn } from '@/lib/utils';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { LoadingState } from '@/components/common/LoadingState';
import { InstructionWithInlineAmounts } from '@/components/recipes/InstructionWithInlineAmounts';
import { RecipeIngredientListRow } from '@/components/recipes/RecipeIngredientListRow';
import { RecipeYieldScale } from '@/components/recipes/RecipeYieldScale';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

function cookingSentenceStorageKey(workspaceId: string, recipeId: string): string {
    return `cooking-mode-sentence-${workspaceId}-${recipeId}`;
}

function collectCookingSentenceIds(steps: { id: string; instruction: string }[]): Set<string> {
    const ids = new Set<string>();
    for (const step of steps) {
        const sentences = splitInstructionIntoSentences(step.instruction);
        const blocks =
            sentences.length > 0 ? sentences : step.instruction.trim() ? [step.instruction] : [''];
        for (let j = 0; j < blocks.length; j++) {
            ids.add(`${step.id}-s${j}`);
        }
    }
    return ids;
}

export default function CookingModePage() {
    const { workspaceId = '', recipeId = '' } = useParams<{ workspaceId: string; recipeId: string }>();
    const { setCurrentWorkspaceId } = useWorkspace();
    const [showIngredients, setShowIngredients] = useState(false);
    const [targetServings, setTargetServings] = useState(1);
    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const recipeStepsRef = useRef<{ id: string; instruction: string }[] | undefined>(undefined);
    const [activeSentenceId, setActiveSentenceId] = useState<string | null>(null);

    useEffect(() => {
        if (workspaceId) {
            try {
                setCurrentWorkspaceId(workspaceId);
            } catch {
                /* invalid workspace */
            }
        }
    }, [setCurrentWorkspaceId, workspaceId]);

    const { data: recipe, isLoading } = useQuery({
        queryKey: ['recipe', workspaceId, recipeId],
        queryFn: () => recipesApi.getById(workspaceId, recipeId),
        enabled: Boolean(workspaceId && recipeId),
    });

    recipeStepsRef.current = recipe?.steps;

    const baseServings = recipe ? (recipe.servings > 0 ? recipe.servings : 1) : 1;

    useEffect(() => {
        if (!recipe) return;
        const base = recipe.servings > 0 ? recipe.servings : 1;
        setTargetServings(Math.min(99, base));
    }, [recipe?.id, recipe?.servings]); // eslint-disable-line react-hooks/exhaustive-deps -- stable deps; `recipe` omitted to avoid reset on cache refresh

    const scaledIngredients = useMemo(() => {
        if (!recipe) return [];
        return scaleRecipeIngredients(recipe.ingredients, baseServings, targetServings);
    }, [recipe, baseServings, targetServings]);

    const stepSentenceSignature = useMemo(
        () => (recipe ? recipe.steps.map(s => `${s.id}:${s.instruction}`).join('\u0001') : ''),
        [recipe],
    );

    useLayoutEffect(() => {
        const root = scrollContainerRef.current;
        const steps = recipeStepsRef.current;
        if (!root || !workspaceId || !recipeId || !stepSentenceSignature || !steps?.length) return;

        const key = cookingSentenceStorageKey(workspaceId, recipeId);
        const validIds = collectCookingSentenceIds(steps);
        const saved = sessionStorage.getItem(key);

        if (saved && validIds.has(saved)) {
            setActiveSentenceId(saved);
            requestAnimationFrame(() => {
                const escaped =
                    typeof CSS !== 'undefined' && typeof CSS.escape === 'function'
                        ? CSS.escape(saved)
                        : saved.replace(/"/g, '\\"');
                const target = root.querySelector<HTMLElement>(`[data-cooking-sentence="${escaped}"]`);
                target?.scrollIntoView({ block: 'center', behavior: 'auto' });
            });
        } else {
            setActiveSentenceId(null);
            if (saved) {
                sessionStorage.removeItem(key);
            }
        }
    }, [recipeId, workspaceId, stepSentenceSignature]);

    useEffect(() => {
        if (!workspaceId || !recipeId) return;
        const key = cookingSentenceStorageKey(workspaceId, recipeId);
        if (activeSentenceId) {
            sessionStorage.setItem(key, activeSentenceId);
        } else {
            sessionStorage.removeItem(key);
        }
    }, [activeSentenceId, workspaceId, recipeId]);

    if (isLoading || !recipe) {
        return (
            <div className='flex min-h-screen items-center justify-center bg-background'>
                <LoadingState label='Loading recipe…' />
            </div>
        );
    }

    const totalSteps = recipe.steps.length;

    const detailPath = `/workspaces/${workspaceId}/recipe/${recipe.id}`;
    const sourceHref = safeHttpUrlHref(recipe.sourceUrl);

    return (
        <div className='flex min-h-screen flex-col bg-background'>
            <div className='sticky top-0 z-20 border-b border-border bg-background/95 px-4 py-3 backdrop-blur-md'>
                <div className='mx-auto flex max-w-2xl items-center justify-between'>
                    <Tooltip>
                        <TooltipTrigger asChild>
                            <Link
                                to={detailPath}
                                className='flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground'
                            >
                                <ArrowLeft className='h-4 w-4' />
                                Exit
                            </Link>
                        </TooltipTrigger>
                        <TooltipContent side='bottom'>Back to recipe</TooltipContent>
                    </Tooltip>
                    <span className='font-heading max-w-[50%] truncate text-center text-foreground'>{recipe.title}</span>
                    <Tooltip>
                        <TooltipTrigger asChild>
                            <button
                                type='button'
                                onClick={() => setShowIngredients(!showIngredients)}
                                aria-expanded={showIngredients}
                                aria-label={showIngredients ? 'Hide ingredients' : 'Show ingredients'}
                                className={`rounded-lg p-2 transition-colors ${
                                    showIngredients ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:text-foreground'
                                }`}
                            >
                                <Carrot className='h-5 w-5' />
                            </button>
                        </TooltipTrigger>
                        <TooltipContent side='bottom'>
                            {showIngredients ? 'Hide ingredients' : 'Show ingredients'}
                        </TooltipContent>
                    </Tooltip>
                </div>

                <div className='mx-auto max-w-2xl px-1 pb-1 pt-2'>
                    <RecipeYieldScale
                        baseServings={baseServings}
                        targetServings={targetServings}
                        onTargetServingsChange={setTargetServings}
                    />
                    {recipe.sourceUrl ? (
                        <div className='flex justify-center pt-2'>
                            {sourceHref ? (
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <a
                                            href={sourceHref}
                                            target='_blank'
                                            rel='noopener noreferrer'
                                            className='inline-flex items-center gap-1 text-xs text-muted-foreground transition-colors hover:text-foreground'
                                        >
                                            <ExternalLink className='h-3.5 w-3.5 shrink-0' aria-hidden />
                                            Original recipe
                                        </a>
                                    </TooltipTrigger>
                                    <TooltipContent side='bottom'>Open original recipe in a new tab</TooltipContent>
                                </Tooltip>
                            ) : (
                                <span className='max-w-full px-2 text-center text-[11px] text-muted-foreground break-all'>
                                    {recipe.sourceUrl}
                                </span>
                            )}
                        </div>
                    ) : null}
                </div>
            </div>

            <AnimatePresence>
                {showIngredients && (
                    <motion.div
                        initial={{ height: 0, opacity: 0 }}
                        animate={{ height: 'auto', opacity: 1 }}
                        exit={{ height: 0, opacity: 0 }}
                        transition={{ duration: 0.25 }}
                        className='overflow-hidden border-b border-border bg-card'
                    >
                        <div className='mx-auto max-w-2xl px-4 py-4'>
                            <h3 className='mb-3 text-sm font-medium text-muted-foreground'>Ingredients</h3>
                            <div className='grid grid-cols-2 gap-x-4 gap-y-1.5'>
                                {scaledIngredients.map(ing => (
                                    <p key={ing.id} className='text-sm text-foreground/90'>
                                        <RecipeIngredientListRow ingredient={ing} />
                                    </p>
                                ))}
                            </div>
                        </div>
                    </motion.div>
                )}
            </AnimatePresence>

            <div ref={scrollContainerRef} className='min-h-0 flex-1 overflow-y-auto px-6 py-8'>
                <div className='mx-auto flex w-full max-w-2xl flex-col gap-10 text-left'>
                    {recipe.steps.map((step, i) => {
                        const sentences = splitInstructionIntoSentences(step.instruction);
                        const blocks =
                            sentences.length > 0
                                ? sentences
                                : step.instruction.trim()
                                  ? [step.instruction]
                                  : [''];

                        return (
                            <section key={step.id} className='scroll-mt-28'>
                                <p className='mb-4 text-xs font-medium uppercase tracking-wider text-primary'>
                                    Step {i + 1} of {totalSteps}
                                </p>
                                <div>
                                    {blocks.map((sentence, j) => {
                                        const sentenceId = `${step.id}-s${j}`;
                                        const isActive = activeSentenceId === sentenceId;
                                        return (
                                            <button
                                                key={sentenceId}
                                                type='button'
                                                data-cooking-sentence={sentenceId}
                                                aria-label={`Mark this instruction as your place (step ${i + 1}, part ${j + 1}). Tap again to clear.`}
                                                aria-current={isActive ? 'true' : undefined}
                                                onClick={() =>
                                                    setActiveSentenceId(current =>
                                                        current === sentenceId ? null : sentenceId,
                                                    )
                                                }
                                                className={cn(
                                                    'block w-full scroll-mt-32 rounded-lg px-2 py-2 text-left font-body text-xl leading-relaxed text-foreground transition-colors duration-200 touch-manipulation md:text-2xl',
                                                    'hover:bg-muted/45 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
                                                    j < blocks.length - 1 && 'mb-3',
                                                    isActive && 'bg-primary/12 ring-2 ring-primary/40 ring-offset-2 ring-offset-background',
                                                )}
                                            >
                                                <InstructionWithInlineAmounts
                                                    instruction={sentence}
                                                    scaledIngredients={scaledIngredients}
                                                />
                                            </button>
                                        );
                                    })}
                                </div>
                            </section>
                        );
                    })}
                </div>
            </div>
        </div>
    );
}
