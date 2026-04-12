import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowLeft, ChevronLeft, ChevronRight, List } from 'lucide-react';
import { recipesApi } from '@/lib/api';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { LoadingState } from '@/components/common/LoadingState';

export default function CookingModePage() {
    const { workspaceId = '', recipeId = '' } = useParams<{ workspaceId: string; recipeId: string }>();
    const { setCurrentWorkspaceId } = useWorkspace();
    const [currentStep, setCurrentStep] = useState(0);
    const [showIngredients, setShowIngredients] = useState(false);

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

    if (isLoading || !recipe) {
        return (
            <div className='flex min-h-screen items-center justify-center bg-background'>
                <LoadingState label='Loading recipe…' />
            </div>
        );
    }

    const totalSteps = recipe.steps.length;
    const isFirst = currentStep === 0;
    const isLast = currentStep === totalSteps - 1;

    const detailPath = `/workspaces/${workspaceId}/recipe/${recipe.id}`;

    return (
        <div className='flex min-h-screen flex-col bg-background'>
            <div className='sticky top-0 z-20 border-b border-border bg-background/95 px-4 py-3 backdrop-blur-md'>
                <div className='mx-auto flex max-w-2xl items-center justify-between'>
                    <Link
                        to={detailPath}
                        className='flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground'
                    >
                        <ArrowLeft className='h-4 w-4' />
                        Exit
                    </Link>
                    <span className='font-heading max-w-[50%] truncate text-center text-foreground'>{recipe.title}</span>
                    <button
                        type='button'
                        onClick={() => setShowIngredients(!showIngredients)}
                        className={`rounded-lg p-2 transition-colors ${
                            showIngredients ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:text-foreground'
                        }`}
                    >
                        <List className='h-5 w-5' />
                    </button>
                </div>

                <div className='mx-auto mt-2 flex max-w-2xl gap-1'>
                    {recipe.steps.map((step, i) => (
                        <div
                            key={step.id}
                            className={`h-1 flex-1 rounded-full transition-colors ${
                                i <= currentStep ? 'bg-primary' : 'bg-secondary'
                            }`}
                        />
                    ))}
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
                                {recipe.ingredients.map(ing => (
                                    <p key={ing.id} className='text-sm'>
                                        <span className='font-medium tabular-nums'>{ing.displayText}</span>
                                    </p>
                                ))}
                            </div>
                        </div>
                    </motion.div>
                )}
            </AnimatePresence>

            <div className='flex flex-1 items-center justify-center px-6 py-8'>
                <div className='w-full max-w-2xl text-center'>
                    <AnimatePresence mode='wait'>
                        <motion.div
                            key={currentStep}
                            initial={{ opacity: 0, x: 40 }}
                            animate={{ opacity: 1, x: 0 }}
                            exit={{ opacity: 0, x: -40 }}
                            transition={{ duration: 0.25 }}
                        >
                            <p className='mb-4 text-xs font-medium uppercase tracking-wider text-primary'>
                                Step {currentStep + 1} of {totalSteps}
                            </p>
                            <p className='font-body text-xl leading-relaxed text-foreground md:text-2xl'>
                                {recipe.steps[currentStep]?.instruction}
                            </p>
                        </motion.div>
                    </AnimatePresence>
                </div>
            </div>

            <div className='safe-area-pb sticky bottom-0 border-t border-border bg-background/95 px-4 py-4 backdrop-blur-md'>
                <div className='mx-auto flex max-w-2xl items-center gap-3'>
                    <button
                        type='button'
                        onClick={() => setCurrentStep(Math.max(0, currentStep - 1))}
                        disabled={isFirst}
                        className='flex flex-1 items-center justify-center gap-2 rounded-xl bg-secondary py-4 text-sm font-medium text-secondary-foreground transition-all active:scale-[0.98] disabled:opacity-30'
                    >
                        <ChevronLeft className='h-5 w-5' />
                        Previous
                    </button>
                    <button
                        type='button'
                        onClick={() => {
                            if (!isLast) setCurrentStep(currentStep + 1);
                        }}
                        className={`flex flex-1 items-center justify-center gap-2 rounded-xl py-4 text-sm font-medium transition-all active:scale-[0.98] ${
                            isLast ? 'bg-primary/10 text-primary' : 'bg-primary text-primary-foreground'
                        }`}
                    >
                        {isLast ? 'Done!' : 'Next'}
                        {!isLast && <ChevronRight className='h-5 w-5' />}
                    </button>
                </div>
            </div>
        </div>
    );
}
