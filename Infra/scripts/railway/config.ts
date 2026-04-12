import fs from 'node:fs';

import YAML from 'yaml';
import { logInfo, logWarn } from './logger';

type ConfigValue = Record<string, unknown>;

export interface RailwayConfig {
    project?: {
        environment?: string;
    };
    app: {
        service: string;
        repo: string;
        variables?: Record<string, string>;
    };
    resources: {
        postgres: {
            service: string;
        };
        redis: {
            service: string;
        };
        bucket: {
            name: string;
            region?: string;
        };
    };
    patch?: {
        skipDeploys?: boolean;
    };
}

export interface ScriptOptions {
    configPath: string;
    dryRun: boolean;
}

export interface EnvironmentScriptOptions extends ScriptOptions {
    envFilePath: string;
}

export function loadConfig(configPath: string): RailwayConfig {
    logInfo(`reading Railway config file ${configPath}`);
    const raw = YAML.parse(fs.readFileSync(configPath, 'utf8')) ?? {};
    const config = raw as RailwayConfig;

    validatePresence(config, ['app', 'service'], 'app.service');
    validatePresence(config, ['app', 'repo'], 'app.repo');
    validatePresence(config, ['resources', 'postgres', 'service'], 'resources.postgres.service');
    validatePresence(config, ['resources', 'redis', 'service'], 'resources.redis.service');
    validatePresence(config, ['resources', 'bucket', 'name'], 'resources.bucket.name');

    logInfo(
        `loaded config for app '${config.app.service}' with repo '${config.app.repo}', postgres '${config.resources.postgres.service}', redis '${config.resources.redis.service}', and bucket '${config.resources.bucket.name}'`,
    );

    return config;
}

export function appServiceName(config: RailwayConfig): string {
    return config.app.service;
}

export function appRepo(config: RailwayConfig): string {
    return config.app.repo;
}

export function postgresServiceName(config: RailwayConfig): string {
    return config.resources.postgres.service;
}

export function redisServiceName(config: RailwayConfig): string {
    return config.resources.redis.service;
}

export function bucketName(config: RailwayConfig): string {
    return config.resources.bucket.name;
}

export function bucketRegion(config: RailwayConfig): string | undefined {
    return config.resources.bucket.region;
}

export function environmentArgs(config: RailwayConfig): string[] {
    const environment = config.project?.environment;

    if (typeof environment !== 'string' || environment.trim() === '') {
        logWarn('no Railway environment configured in project.environment; using the active linked CLI context');
        return [];
    }

    logInfo(`using Railway environment '${environment}'`);
    return ['-e', environment];
}

export function appVariables(config: RailwayConfig): Record<string, string> {
    return {
        ...generatedAppVariables(config),
        ...stringMap(config.app.variables),
    };
}

export function shouldSkipDeploys(config: RailwayConfig): boolean {
    return Boolean(config.patch?.skipDeploys);
}

export function commandString(args: string[]): string {
    return args
        .map(value => {
            if (/^[A-Za-z0-9_./:@-]+$/.test(value)) {
                return value;
            }

            return JSON.stringify(value);
        })
        .join(' ');
}

function generatedAppVariables(config: RailwayConfig): Record<string, string> {
    const postgresService = postgresServiceName(config);
    const redisService = redisServiceName(config);
    const bucket = bucketName(config);

    return {
        ConnectionStrings__DefaultConnection: serviceVariable(postgresService, 'DATABASE_URL'),
        ConnectionStrings__Redis: serviceVariable(redisService, 'REDIS_URL'),
        S3__BucketName: serviceVariable(bucket, 'BUCKET'),
        S3__AccessKey: serviceVariable(bucket, 'ACCESS_KEY_ID'),
        S3__SecretKey: serviceVariable(bucket, 'SECRET_ACCESS_KEY'),
        S3__Region: serviceVariable(bucket, 'REGION'),
        S3__ServiceUrl: serviceVariable(bucket, 'ENDPOINT'),
    };
}

function serviceVariable(resourceName: string, variableName: string): string {
    return `\${{${resourceName}.${variableName}}}`;
}

function stringMap(value: Record<string, string> | undefined): Record<string, string> {
    if (!value) {
        return {};
    }

    return Object.fromEntries(Object.entries(value).map(([key, mapValue]) => [String(key), String(mapValue)]));
}

function validatePresence(config: RailwayConfig, keys: string[], label: string): void {
    const value = dig(config as unknown as ConfigValue, ...keys);

    if (isBlank(value)) {
        throw new Error(`Missing required config value '${label}'`);
    }
}

function dig(value: ConfigValue | undefined, ...keys: string[]): unknown {
    return keys.reduce<unknown>((current, key) => {
        if (!current || typeof current !== 'object') {
            return undefined;
        }

        return (current as ConfigValue)[key];
    }, value);
}

function isBlank(value: unknown): boolean {
    return value == null || String(value).trim() === '';
}
