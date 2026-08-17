import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2 } from 'lucide-react';
import { recipeCollectionsApi } from '@/lib/api';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { Button } from '@/components/ui/button';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Progress } from '@/components/ui/progress';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState } from '@/components/common/EmptyState';
import { toast } from '@/hooks/use-toast';
import { useAnalytics, analyticsEvents } from '@/lib/analytics';
import {
    isRecipeCollectionImportJobActive,
    recipeCollectionImportJobStatuses,
    type RecipeCollectionImportJob,
} from '@/models/meal-prep';

const JOB_POLL_INTERVAL_MS = 1500;

export default function RecipeCollectionShareImportPage() {
    const { shareToken = '' } = useParams<{ shareToken: string }>();
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { capture } = useAnalytics();
    const { workspaces, currentWorkspace } = useWorkspace();
    const [targetWorkspaceId, setTargetWorkspaceId] = useState(currentWorkspace?.workspaceId ?? '');
    const [activeJobId, setActiveJobId] = useState<string | null>(null);
    const completionReportedFor = useRef<string | null>(null);

    const { data: preview, isLoading } = useQuery({
        queryKey: ['recipe-collection-share-preview', shareToken],
        queryFn: () => recipeCollectionsApi.getShareLinkPreview(shareToken),
        enabled: Boolean(shareToken),
    });

    // An import started before a reload is still on the server, so the page picks it back up.
    const { data: existingJobs } = useQuery({
        queryKey: ['recipe-collection-import-jobs', targetWorkspaceId, shareToken],
        queryFn: () => recipeCollectionsApi.listImportJobs(targetWorkspaceId, shareToken),
        enabled: Boolean(targetWorkspaceId && shareToken),
    });

    const { data: job } = useQuery({
        queryKey: ['recipe-collection-import-job', targetWorkspaceId, activeJobId],
        queryFn: () => recipeCollectionsApi.getImportJob(targetWorkspaceId, activeJobId ?? ''),
        enabled: Boolean(targetWorkspaceId && activeJobId),
        refetchInterval: query =>
            isRecipeCollectionImportJobActive(query.state.data as RecipeCollectionImportJob | undefined)
                ? JOB_POLL_INTERVAL_MS
                : false,
    });

    useEffect(() => {
        if (activeJobId || !existingJobs?.length) return;

        const latest = existingJobs[0];
        if (latest.status === recipeCollectionImportJobStatuses.completed) return;

        setActiveJobId(latest.id);
    }, [activeJobId, existingJobs]);

    useEffect(() => {
        if (!job || isRecipeCollectionImportJobActive(job)) return;
        if (completionReportedFor.current === `${job.id}:${job.completedAt}`) return;

        completionReportedFor.current = `${job.id}:${job.completedAt}`;

        capture(analyticsEvents.recipeCollectionImportFinished, {
            workspace_id: job.workspaceId,
            import_job_id: job.id,
            status: job.status,
            total_recipes: job.totalRecipes,
            imported_recipes: job.importedRecipes,
            failed_recipes: job.failedRecipes,
        });

        if (job.status === recipeCollectionImportJobStatuses.completed && job.targetCollectionId) {
            toast({ title: 'Collection imported', description: `${job.importedRecipes} recipes added` });
            navigate(`/workspaces/${job.workspaceId}/collections/${job.targetCollectionId}`);
        }
    }, [capture, job, navigate]);

    const startImportMutation = useMutation({
        mutationFn: () => recipeCollectionsApi.startImportJob(targetWorkspaceId, shareToken),
        onSuccess: startedJob => {
            completionReportedFor.current = null;
            setActiveJobId(startedJob.id);
            queryClient.setQueryData(
                ['recipe-collection-import-job', targetWorkspaceId, startedJob.id],
                startedJob,
            );
            capture(analyticsEvents.recipeCollectionImportStarted, {
                workspace_id: targetWorkspaceId,
                import_job_id: startedJob.id,
                total_recipes: startedJob.totalRecipes,
            });
        },
        onError: () => {
            toast({ title: 'Import failed to start', variant: 'destructive' });
        },
    });

    const retryImportMutation = useMutation({
        mutationFn: () => recipeCollectionsApi.retryImportJob(targetWorkspaceId, activeJobId ?? ''),
        onSuccess: retriedJob => {
            completionReportedFor.current = null;
            queryClient.setQueryData(
                ['recipe-collection-import-job', targetWorkspaceId, retriedJob.id],
                retriedJob,
            );
            capture(analyticsEvents.recipeCollectionImportRetried, {
                workspace_id: targetWorkspaceId,
                import_job_id: retriedJob.id,
                retried_recipes: retriedJob.totalRecipes - retriedJob.importedRecipes,
            });
        },
        onError: () => {
            toast({ title: 'Retry failed to start', variant: 'destructive' });
        },
    });

    const selectableWorkspaces = useMemo(() => workspaces, [workspaces]);
    const isJobActive = isRecipeCollectionImportJobActive(job);
    const progressPercent = job && job.totalRecipes > 0
        ? Math.round((job.processedRecipes / job.totalRecipes) * 100)
        : 0;

    if (isLoading) {
        return (
            <div className='mx-auto max-w-xl px-4 py-10 md:px-8'>
                <LoadingState label='Loading share link…' />
            </div>
        );
    }

    if (!preview) {
        return (
            <div className='mx-auto max-w-xl px-4 py-10 md:px-8'>
                <EmptyState title='Share link not found' description='The link may be invalid or expired.' />
            </div>
        );
    }

    return (
        <div className='mx-auto max-w-xl px-4 py-10 md:px-8'>
            <div className='rounded-xl border border-border bg-card p-6'>
                <h1 className='font-heading text-2xl text-foreground'>Import shared collection</h1>
                <p className='mt-2 text-sm text-muted-foreground'>
                    <span className='font-medium text-foreground'>{preview.collectionName}</span> from{' '}
                    {preview.ownerWorkspaceName}
                </p>
                <p className='mt-1 text-sm text-muted-foreground'>{preview.recipeCount} recipes</p>

                {!job && (
                    <div className='mt-5 space-y-3'>
                        <label className='text-sm font-medium text-foreground' htmlFor='import-workspace'>
                            Import into workspace
                        </label>
                        <Select value={targetWorkspaceId} onValueChange={setTargetWorkspaceId}>
                            <SelectTrigger id='import-workspace'>
                                <SelectValue placeholder='Choose workspace…' />
                            </SelectTrigger>
                            <SelectContent>
                                {selectableWorkspaces.map(workspace => (
                                    <SelectItem key={workspace.workspaceId} value={workspace.workspaceId}>
                                        {workspace.name}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>
                )}

                {job && (
                    <div className='mt-6' data-testid='import-progress' role='status' aria-live='polite'>
                        <div className='flex items-baseline justify-between gap-3'>
                            <span className='text-sm font-medium text-foreground'>
                                {isJobActive ? 'Importing recipes' : importOutcomeLabel(job)}
                            </span>
                            <span className='whitespace-nowrap text-sm text-muted-foreground'>
                                {job.processedRecipes} of {job.totalRecipes}
                            </span>
                        </div>

                        <Progress
                            className='mt-3 h-2'
                            value={progressPercent}
                            aria-label='Collection import progress'
                        />

                        {isJobActive && (
                            <p className='mt-3 text-sm text-muted-foreground'>
                                This keeps running if you close the page — come back to this link to check on it.
                            </p>
                        )}

                        {job.status === recipeCollectionImportJobStatuses.failed && (
                            <p className='mt-3 flex items-start gap-2 text-sm text-destructive'>
                                <AlertTriangle className='mt-0.5 h-4 w-4 shrink-0' />
                                <span>{job.errorMessage ?? 'The import stopped unexpectedly.'}</span>
                            </p>
                        )}

                        {job.failures.length > 0 && (
                            <div className='mt-5 rounded-lg border border-destructive/40 bg-destructive/5 p-4'>
                                <p className='flex items-center gap-2 text-sm font-medium text-foreground'>
                                    <AlertTriangle className='h-4 w-4 text-destructive' />
                                    {job.failedRecipes} of {job.totalRecipes} recipes could not be imported
                                </p>
                                <ul className='mt-3 space-y-2' data-testid='import-failures'>
                                    {job.failures.map(failure => (
                                        <li key={failure.sourceRecipeId} className='text-sm'>
                                            <span className='font-medium text-foreground'>{failure.recipeTitle}</span>
                                            {failure.errorMessage && (
                                                <span className='block text-xs text-muted-foreground'>
                                                    {failure.errorMessage}
                                                </span>
                                            )}
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        )}

                        {job.status === recipeCollectionImportJobStatuses.completed && (
                            <p className='mt-3 flex items-center gap-2 text-sm text-muted-foreground'>
                                <CheckCircle2 className='h-4 w-4 text-primary' />
                                All {job.importedRecipes} recipes imported.
                            </p>
                        )}
                    </div>
                )}

                <div className='mt-6 flex flex-col gap-3'>
                    {!job && (
                        <Button
                            type='button'
                            className='w-full'
                            disabled={!targetWorkspaceId || startImportMutation.isPending}
                            onClick={() => startImportMutation.mutate()}
                        >
                            {startImportMutation.isPending ? 'Starting import…' : 'Import collection'}
                        </Button>
                    )}

                    {job && !isJobActive && job.status !== recipeCollectionImportJobStatuses.completed && (
                        <Button
                            type='button'
                            className='w-full'
                            disabled={retryImportMutation.isPending}
                            onClick={() => retryImportMutation.mutate()}
                        >
                            {retryImportMutation.isPending ? 'Retrying…' : 'Retry failed recipes'}
                        </Button>
                    )}

                    {job?.targetCollectionId && (
                        <Button
                            type='button'
                            variant='outline'
                            className='w-full'
                            onClick={() =>
                                navigate(`/workspaces/${job.workspaceId}/collections/${job.targetCollectionId}`)
                            }
                        >
                            View imported collection
                        </Button>
                    )}
                </div>
            </div>
        </div>
    );
}

function importOutcomeLabel(job: RecipeCollectionImportJob) {
    if (job.status === recipeCollectionImportJobStatuses.completedWithErrors) return 'Imported with errors';
    if (job.status === recipeCollectionImportJobStatuses.failed) return 'Import stopped';
    return 'Import finished';
}
