import { spawnSync } from 'node:child_process';

import { commandString, type ScriptOptions } from './config';
import { logInfo, logSuccess } from './logger';

export function runCommand(args: string[], options: ScriptOptions): void {
    logInfo(`running command: ${commandString(args)}`);

    if (options.dryRun) {
        logInfo('dry-run enabled, command was not executed');
        return;
    }

    const result = spawnSync(args[0], args.slice(1), {
        stdio: 'inherit',
    });

    if (result.error) {
        throw result.error;
    }

    if (result.status !== 0) {
        throw new Error(`Command failed with exit code ${result.status}: ${commandString(args)}`);
    }

    logSuccess(`command completed: ${args[0]}`);
}
