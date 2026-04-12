const reset = '\x1b[0m';
const blue = '\x1b[34m';
const yellow = '\x1b[33m';
const green = '\x1b[32m';
const red = '\x1b[31m';
const dim = '\x1b[2m';

export function logInfo(message: string): void {
    console.log(`${blue}[infra]${reset} ${message}`);
}

export function logWarn(message: string): void {
    console.warn(`${yellow}[infra] warning:${reset} ${message}`);
}

export function logSuccess(message: string): void {
    console.log(`${green}[infra] success:${reset} ${message}`);
}

export function logError(message: string, error?: unknown): void {
    console.error(`${red}[infra] error:${reset} ${message}`);

    if (error instanceof Error) {
        console.error(`${red}[infra] error detail:${reset} ${error.message}`);

        if (error.stack) {
            console.error(`${dim}${error.stack}${reset}`);
        }

        return;
    }

    if (error != null) {
        console.error(error);
    }
}
