import process from 'node:process';
import { Command } from 'commander';

import {
    appServiceName,
    appVariables,
    environmentArgs,
    loadConfig,
    shouldSkipDeploys,
    type ScriptOptions,
} from './config';
import { normalizedArgv } from './command-options';
import { logError, logInfo, logSuccess } from './logger';
import { runCommand } from './run-command';

main();

function main(): void {
    try {
        const options = parseArgs();
        logInfo(`loading Railway resource config from ${options.configPath}`);

        const config = loadConfig(options.configPath);
        const variableArgs = Object.entries(appVariables(config)).map(([key, value]) => `${key}=${value}`);
        const command = ['railway', 'variable', 'set', '-s', appServiceName(config), ...environmentArgs(config)];

        if (shouldSkipDeploys(config)) {
            command.push('--skip-deploys');
        }

        command.push(...variableArgs);

        logInfo(`patching ${variableArgs.length} app variables for service '${appServiceName(config)}'`);
        runCommand(command, options);

        logSuccess('patch command completed');
    } catch (error) {
        logError('patch failed', error);
        process.exitCode = 1;
    }
}

function parseArgs(): ScriptOptions {
    const program = new Command();

    program
        .name('railway:patch')
        .description('Apply Railway-managed environment variables to the app service')
        .option('-c, --config <path>', 'path to the Railway resource config YAML', 'railway.resources.yml')
        .option('--dry-run', 'print commands without executing them', false)
        .parse(normalizedArgv());

    const parsed = program.opts<{ config: string; dryRun: boolean }>();

    return {
        configPath: parsed.config,
        dryRun: parsed.dryRun,
    };
}
