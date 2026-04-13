import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowLeft, Carrot, ExternalLink } from 'lucide-react';
import { recipesApi } from '@/lib/api';
import { safeHttpUrlHref, scaleRecipeIngredients } from '@/lib/meal-prep';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { LoadingState } from '@/components/common/LoadingState';
import { InstructionWithInlineAmounts } from '@/components/recipes/InstructionWithInlineAmounts';
import { RecipeIngredientListRow } from '@/components/recipes/RecipeIngredientListRow';
import { RecipeYieldScale } from '@/components/recipes/RecipeYieldScale';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

export default function CookingModePage() {
    const { workspaceId = '', recipeId = '' } = useParams<{ workspaceId: string; recipeId: string }>();
    const { setCurrentWorkspaceId } = useWorkspace();
    const [showIngredients, setShowIngredients] = useState(false);
    const [targetServings, setTargetServings] = useState(1);

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

            <div className='min-h-0 flex-1 overflow-y-auto px-6 py-8'>
                <div className='mx-auto flex w-full max-w-2xl flex-col gap-10 text-left'>
                    {recipe.steps.map((step, i) => (
                        <section key={step.id} className='scroll-mt-28'>
                            <p className='mb-4 text-xs font-medium uppercase tracking-wider text-primary'>
                                Step {i + 1} of {totalSteps}
                            </p>
                            <p className='font-body text-xl leading-relaxed text-foreground md:text-2xl'>
                                <InstructionWithInlineAmounts
                                    instruction={step.instruction}
                                    scaledIngredients={scaledIngredients}
                                />
                            </p>
                        </section>
                    ))}
                </div>
            </div>
        </div>
    );
}
