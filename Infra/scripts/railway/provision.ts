import process from 'node:process';
import { Command } from 'commander';

import {
    appRepo,
    appServiceName,
    bucketName,
    bucketRegion,
    environmentArgs,
    loadConfig,
    postgresServiceName,
    redisServiceName,
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

        logInfo(`provisioning app service '${appServiceName(config)}' from repo '${appRepo(config)}'`);
        runCommand(['railway', 'add', '-s', appServiceName(config), '-r', appRepo(config)], options);

        logInfo(`provisioning Postgres service '${postgresServiceName(config)}'`);
        runCommand(['railway', 'add', '-d', 'postgres', '-s', postgresServiceName(config)], options);

        logInfo(`provisioning Redis service '${redisServiceName(config)}'`);
        runCommand(['railway', 'add', '-d', 'redis', '-s', redisServiceName(config)], options);

        const bucketCommand = ['railway', 'bucket', 'create', bucketName(config)];
        const region = bucketRegion(config);

        if (region) {
            bucketCommand.push('-r', region);
        }

        bucketCommand.push(...environmentArgs(config));

        logInfo(`provisioning bucket '${bucketName(config)}'`);
        runCommand(bucketCommand, options);

        logSuccess('provisioning commands completed');
        logInfo(`run pnpm railway:patch -- --config ${options.configPath} next to wire service variables`);
    } catch (error) {
        logError('provisioning failed', error);
        process.exitCode = 1;
    }
}

function parseArgs(): ScriptOptions {
    const program = new Command();

    program
        .name('railway:provision')
        .description('Provision the Railway app service, Postgres, Redis, and bucket')
        .option('-c, --config <path>', 'path to the Railway resource config YAML', 'railway.resources.yml')
        .option('--dry-run', 'print commands without executing them', false)
        .parse(normalizedArgv());

    const parsed = program.opts<{ config: string; dryRun: boolean }>();

    return {
        configPath: parsed.config,
        dryRun: parsed.dryRun,
    };
}
