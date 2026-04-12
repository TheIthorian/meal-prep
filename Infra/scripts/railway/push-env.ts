import process from 'node:process';
import { Command } from 'commander';

import { normalizedArgv } from './command-options';
import { loadEnvironmentFile } from './env-files';
import {
    appServiceName,
    environmentArgs,
    loadConfig,
    shouldSkipDeploys,
    type EnvironmentScriptOptions,
} from './config';
import { logError, logInfo, logSuccess } from './logger';
import { runCommand } from './run-command';

main();

function main(): void {
    try {
        const options = parseArgs();
        logInfo(`loading Railway resource config from ${options.configPath}`);

        const config = loadConfig(options.configPath);
        logInfo(`loading environment variables from ${options.envFilePath}`);

        const environmentVariables = loadEnvironmentFile(options.envFilePath);
        const variableArgs = Object.entries(environmentVariables).map(([key, value]) => `${key}=${value}`);
        const command = ['railway', 'variable', 'set', '-s', appServiceName(config), ...environmentArgs(config)];

        if (shouldSkipDeploys(config)) {
            command.push('--skip-deploys');
        }

        command.push(...variableArgs);

        logInfo(`pushing ${variableArgs.length} environment variables to service '${appServiceName(config)}'`);
        runCommand(command, options);

        logSuccess(`pushed ${variableArgs.length} environment variables from ${options.envFilePath}`);
    } catch (error) {
        logError('environment push failed', error);
        process.exitCode = 1;
    }
}

function parseArgs(): EnvironmentScriptOptions {
    const program = new Command();

    program
        .name('railway:push-env')
        .description('Push environment variables from a local env file to Railway')
        .option('-c, --config <path>', 'path to the Railway resource config YAML', 'railway.resources.yml')
        .option('-f, --env-file <path>', 'path to the env file to push', 'environments/production.env')
        .option('--dry-run', 'print commands without executing them', false)
        .parse(normalizedArgv());

    const parsed = program.opts<{ config: string; envFile: string; dryRun: boolean }>();

    return {
        configPath: parsed.config,
        dryRun: parsed.dryRun,
        envFilePath: parsed.envFile,
    };
}
