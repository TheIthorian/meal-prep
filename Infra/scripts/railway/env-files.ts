import fs from 'node:fs';
import path from 'node:path';

import dotenv from 'dotenv';
import { logInfo } from './logger';

export function loadEnvironmentFile(envFilePath: string): Record<string, string> {
    const absolutePath = path.resolve(envFilePath);

    logInfo(`resolving environment file ${absolutePath}`);

    if (!fs.existsSync(absolutePath)) {
        throw new Error(`Environment file not found: ${absolutePath}`);
    }

    const parsed = dotenv.parse(fs.readFileSync(absolutePath, 'utf8'));

    logInfo(`loaded ${Object.keys(parsed).length} environment variables from ${absolutePath}`);

    return Object.fromEntries(Object.entries(parsed).map(([key, value]) => [String(key), String(value)]));
}
