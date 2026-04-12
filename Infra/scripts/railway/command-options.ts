import process from 'node:process';

export function normalizedArgv(): string[] {
    const args = [...process.argv];
    const separatorIndex = args.indexOf('--');

    if (separatorIndex === -1) {
        return args;
    }

    return [...args.slice(0, separatorIndex), ...args.slice(separatorIndex + 1)];
}
