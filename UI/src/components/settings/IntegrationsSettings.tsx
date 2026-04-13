import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { format, parseISO } from 'date-fns';
import { Check, Copy, Trash2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { mcpAccessTokensApi, workspacesApi } from '@/lib/api';
import { useWorkspace } from '@/contexts/WorkspaceContext';
import { useAuth } from '@/contexts/AuthContext';
import { toast } from '@/hooks/use-toast';
import { useIsMobile } from '@/hooks/use-mobile';
import { analyticsEvents, useAnalytics, withWorkspaceProperties } from '@/lib/analytics';
import type { McpAccessTokenCreated, McpAccessTokenListItem } from '@/models/mcp';
import { cn } from '@/lib/utils';

function formatTokenTimestamp(iso: string) {
    try {
        return format(parseISO(iso), 'MMM d, yyyy');
    } catch {
        return iso;
    }
}

export function IntegrationsSettings() {
    const queryClient = useQueryClient();
    const { currentWorkspace } = useWorkspace();
    const { user } = useAuth();
    const { capture } = useAnalytics();
    const isMobile = useIsMobile();

    const [selectedWorkspaceId, setSelectedWorkspaceId] = useState('');
    const [optionalLabel, setOptionalLabel] = useState('');
    const [createdConnection, setCreatedConnection] = useState<McpAccessTokenCreated | null>(null);
    const [urlCopied, setUrlCopied] = useState(false);
    const [tokenToRevoke, setTokenToRevoke] = useState<McpAccessTokenListItem | null>(null);

    const { data: workspaces = [] } = useQuery({
        queryKey: ['workspaces'],
        queryFn: () => workspacesApi.getAll(),
    });

    const { data: tokens = [], isLoading: tokensLoading } = useQuery({
        queryKey: ['mcp-access-tokens'],
        queryFn: () => mcpAccessTokensApi.list(),
    });

    useEffect(() => {
        if (selectedWorkspaceId) return;
        if (currentWorkspace?.workspaceId) {
            setSelectedWorkspaceId(currentWorkspace.workspaceId);
            return;
        }
        if (workspaces[0]) {
            setSelectedWorkspaceId(workspaces[0].id);
        }
    }, [currentWorkspace?.workspaceId, selectedWorkspaceId, workspaces]);

    const workspaceNameById = useMemo(() => {
        const map = new Map<string, string>();
        for (const w of workspaces) {
            map.set(w.id, w.name);
        }
        return map;
    }, [workspaces]);

    const createToken = useMutation({
        mutationFn: () => {
            const trimmed = optionalLabel.trim();
            return mcpAccessTokensApi.create({
                workspaceId: selectedWorkspaceId,
                ...(trimmed ? { name: trimmed } : {}),
            });
        },
        onSuccess: data => {
            const workspaceMeta = user?.workspaces.find(w => w.workspaceId === data.workspaceId);
            capture(analyticsEvents.mcpAccessTokenCreated, withWorkspaceProperties(workspaceMeta));
            queryClient.invalidateQueries({ queryKey: ['mcp-access-tokens'] });
            setCreatedConnection(data);
            setUrlCopied(false);
            setOptionalLabel('');
        },
    });

    const revokeToken = useMutation({
        mutationFn: (tokenId: string) => mcpAccessTokensApi.revoke(tokenId),
        onSuccess: (_data, tokenId) => {
            const row = tokens.find(t => t.id === tokenId);
            const workspaceMeta = row ? user?.workspaces.find(w => w.workspaceId === row.workspaceId) : undefined;
            capture(analyticsEvents.mcpAccessTokenRevoked, withWorkspaceProperties(workspaceMeta));
            queryClient.invalidateQueries({ queryKey: ['mcp-access-tokens'] });
            toast({ title: 'Connection revoked' });
            setTokenToRevoke(null);
        },
    });

    async function copyMcpUrl(url: string) {
        try {
            await navigator.clipboard.writeText(url);
            setUrlCopied(true);
            toast({ title: 'Copied', description: 'MCP server URL copied to clipboard.' });
        } catch {
            toast({
                title: 'Copy failed',
                description: 'Select the URL and copy it manually.',
                variant: 'destructive',
            });
        }
    }

    const sortedTokens = useMemo(
        () => [...tokens].sort((a, b) => parseISO(b.createdAt).getTime() - parseISO(a.createdAt).getTime()),
        [tokens],
    );

    return (
        <>
            <div className='space-y-6'>
                <Card className='overflow-hidden border-border/80 shadow-sm'>
                    <CardHeader className='border-b border-border/60 bg-muted/20'>
                        <div className='flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between'>
                            <div className='flex min-w-0 flex-1 items-start gap-4'>
                                <div className='shrink-0 pt-0.5'>
                                    <img
                                        src='/mcp-logo-light.svg'
                                        alt='Model Context Protocol'
                                        className='h-8 w-auto max-w-[min(100%,280px)] dark:hidden'
                                    />
                                    <img
                                        src='/mcp-logo-dark.svg'
                                        alt=''
                                        className='hidden h-8 w-auto max-w-[min(100%,280px)] dark:block'
                                        aria-hidden
                                    />
                                </div>
                                <div className='min-w-0'>
                                    <CardTitle className='text-lg'>MCP server</CardTitle>
                                    <CardDescription className='mt-1.5'>
                                        Connect Cursor, Claude, or other MCP clients to your Meal Prep workspace using a
                                        personal server URL. Each URL is scoped to one workspace and can be revoked
                                        anytime.
                                    </CardDescription>
                                </div>
                            </div>
                        </div>
                    </CardHeader>
                    <CardContent className='space-y-6 pt-6'>
                        {workspaces.length === 0 ? (
                            <p className='text-sm text-muted-foreground'>
                                Create a workspace first, then you can generate an MCP server URL for it.
                            </p>
                        ) : (
                            <>
                                <div className='grid gap-4 sm:max-w-md'>
                                    <div className='space-y-2'>
                                        <Label htmlFor='mcp-workspace'>Workspace</Label>
                                        <Select
                                            value={selectedWorkspaceId}
                                            onValueChange={setSelectedWorkspaceId}
                                            disabled={createToken.isPending}
                                        >
                                            <SelectTrigger id='mcp-workspace'>
                                                <SelectValue placeholder='Choose workspace' />
                                            </SelectTrigger>
                                            <SelectContent>
                                                {workspaces.map(w => (
                                                    <SelectItem key={w.id} value={w.id}>
                                                        {w.name}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <div className='space-y-2'>
                                        <Label htmlFor='mcp-label'>Label (optional)</Label>
                                        <Input
                                            id='mcp-label'
                                            placeholder='e.g. Cursor on laptop'
                                            maxLength={128}
                                            value={optionalLabel}
                                            onChange={e => setOptionalLabel(e.target.value)}
                                            disabled={createToken.isPending}
                                        />
                                        <p className='text-xs text-muted-foreground'>
                                            Helps you tell connections apart in the list below.
                                        </p>
                                    </div>
                                    <Button
                                        type='button'
                                        disabled={!selectedWorkspaceId || createToken.isPending}
                                        onClick={() => createToken.mutate()}
                                    >
                                        {createToken.isPending ? 'Generating…' : 'Generate MCP URL'}
                                    </Button>
                                </div>

                                <div className='space-y-3'>
                                    <h3 className='text-sm font-medium text-foreground'>Active connections</h3>
                                    {tokensLoading ? (
                                        <p className='text-sm text-muted-foreground'>Loading…</p>
                                    ) : sortedTokens.length === 0 ? (
                                        <p className='text-sm text-muted-foreground'>
                                            No MCP connections yet. Generate a URL to get started.
                                        </p>
                                    ) : isMobile ? (
                                        <ul className='space-y-3'>
                                            {sortedTokens.map(row => (
                                                <li
                                                    key={row.id}
                                                    className={cn(
                                                        'rounded-lg border p-3',
                                                        row.revokedAt && 'opacity-60',
                                                    )}
                                                >
                                                    <div className='flex items-start justify-between gap-2'>
                                                        <div className='min-w-0'>
                                                            <p className='font-medium'>
                                                                {row.name?.trim() || 'Unnamed'}
                                                            </p>
                                                            <p className='text-sm text-muted-foreground'>
                                                                {workspaceNameById.get(row.workspaceId) ??
                                                                    row.workspaceId}
                                                            </p>
                                                            <p className='mt-1 text-xs text-muted-foreground'>
                                                                Created {formatTokenTimestamp(row.createdAt)}
                                                                {row.lastUsedAt
                                                                    ? ` · Last used ${formatTokenTimestamp(row.lastUsedAt)}`
                                                                    : ''}
                                                            </p>
                                                        </div>
                                                        <div className='flex shrink-0 flex-col items-end gap-2'>
                                                            {row.revokedAt ? (
                                                                <Badge variant='secondary'>Revoked</Badge>
                                                            ) : (
                                                                <Button
                                                                    variant='ghost'
                                                                    size='icon'
                                                                    className='h-8 w-8'
                                                                    onClick={() => setTokenToRevoke(row)}
                                                                    disabled={revokeToken.isPending}
                                                                    aria-label='Revoke connection'
                                                                >
                                                                    <Trash2 className='h-4 w-4 text-destructive' />
                                                                </Button>
                                                            )}
                                                        </div>
                                                    </div>
                                                </li>
                                            ))}
                                        </ul>
                                    ) : (
                                        <Table>
                                            <TableHeader>
                                                <TableRow>
                                                    <TableHead>Label</TableHead>
                                                    <TableHead>Workspace</TableHead>
                                                    <TableHead>Created</TableHead>
                                                    <TableHead>Last used</TableHead>
                                                    <TableHead className='w-[100px]'>Status</TableHead>
                                                    <TableHead className='w-[80px]' />
                                                </TableRow>
                                            </TableHeader>
                                            <TableBody>
                                                {sortedTokens.map(row => (
                                                    <TableRow
                                                        key={row.id}
                                                        className={row.revokedAt ? 'opacity-60' : undefined}
                                                    >
                                                        <TableCell className='font-medium'>
                                                            {row.name?.trim() || 'Unnamed'}
                                                        </TableCell>
                                                        <TableCell>
                                                            {workspaceNameById.get(row.workspaceId) ?? row.workspaceId}
                                                        </TableCell>
                                                        <TableCell className='whitespace-nowrap'>
                                                            {formatTokenTimestamp(row.createdAt)}
                                                        </TableCell>
                                                        <TableCell className='whitespace-nowrap'>
                                                            {row.lastUsedAt
                                                                ? formatTokenTimestamp(row.lastUsedAt)
                                                                : '—'}
                                                        </TableCell>
                                                        <TableCell>
                                                            {row.revokedAt ? (
                                                                <Badge variant='secondary'>Revoked</Badge>
                                                            ) : (
                                                                <Badge variant='outline'>Active</Badge>
                                                            )}
                                                        </TableCell>
                                                        <TableCell>
                                                            {!row.revokedAt && (
                                                                <Button
                                                                    variant='ghost'
                                                                    size='icon'
                                                                    onClick={() => setTokenToRevoke(row)}
                                                                    disabled={revokeToken.isPending}
                                                                    aria-label='Revoke connection'
                                                                >
                                                                    <Trash2 className='h-4 w-4 text-destructive' />
                                                                </Button>
                                                            )}
                                                        </TableCell>
                                                    </TableRow>
                                                ))}
                                            </TableBody>
                                        </Table>
                                    )}
                                </div>
                            </>
                        )}
                    </CardContent>
                </Card>
            </div>

            <Dialog
                open={createdConnection !== null}
                onOpenChange={open => {
                    if (!open) {
                        setCreatedConnection(null);
                        setUrlCopied(false);
                    }
                }}
            >
                <DialogContent className='sm:max-w-lg'>
                    <DialogHeader>
                        <DialogTitle>MCP server URL</DialogTitle>
                        <DialogDescription>
                            Add this URL in your MCP client as the server address. Store it securely; anyone with the
                            URL can access this workspace through Meal Prep.
                        </DialogDescription>
                    </DialogHeader>
                    <div className='space-y-2'>
                        <Label htmlFor='mcp-url-copy'>Server URL</Label>
                        <div className='flex min-w-0 gap-2'>
                            <Input
                                id='mcp-url-copy'
                                readOnly
                                value={createdConnection?.mcpUrl ?? ''}
                                className='min-w-0 flex-1 font-mono text-xs'
                            />
                            <Button
                                type='button'
                                variant='secondary'
                                className='shrink-0 gap-1.5'
                                onClick={() => createdConnection && copyMcpUrl(createdConnection.mcpUrl)}
                            >
                                {urlCopied ? <Check className='h-4 w-4' /> : <Copy className='h-4 w-4' />}
                                Copy
                            </Button>
                        </div>
                    </div>
                    <DialogFooter>
                        <Button type='button' onClick={() => setCreatedConnection(null)}>
                            Done
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            <AlertDialog open={tokenToRevoke !== null} onOpenChange={open => !open && setTokenToRevoke(null)}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Revoke this connection?</AlertDialogTitle>
                        <AlertDialogDescription>
                            Clients using this MCP URL will lose access immediately. This cannot be undone.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <AlertDialogAction
                            className='bg-destructive text-destructive-foreground hover:bg-destructive/90'
                            onClick={() => tokenToRevoke && revokeToken.mutate(tokenToRevoke.id)}
                            disabled={revokeToken.isPending}
                        >
                            {revokeToken.isPending ? 'Revoking…' : 'Revoke'}
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </>
    );
}
