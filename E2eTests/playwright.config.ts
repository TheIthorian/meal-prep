import { defineConfig, devices } from '@playwright/test';
import dotenv from 'dotenv';

dotenv.config();

const baseURL = process.env.PLAYWRIGHT_BASE_URL || process.env.BASE_URL;

if (!baseURL) {
    throw new Error(
        'PLAYWRIGHT_BASE_URL (or BASE_URL) must be set. Example: PLAYWRIGHT_BASE_URL=https://your-app-url npm test',
    );
}

export default defineConfig({
    testDir: './tests',
    timeout: 60_000,
    expect: {
        timeout: 15_000,
        toHaveScreenshot: {
            // Single baseline across platforms so CI (Linux) and local (darwin) use the same snapshots.
            pathTemplate: '{testDir}/{testFilePath}-snapshots/{arg}-{projectName}{ext}',
        },
    },
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 2 : 0,
    workers: process.env.CI ? 2 : undefined,
    reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
    use: {
        baseURL,
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
        video: 'retain-on-failure',
        ignoreHTTPSErrors: true,
        viewport: { width: 1440, height: 900 },
        colorScheme: 'light',
        locale: 'en-US',
    },
    projects: [
        {
            name: 'chromium',
            use: { ...devices['Desktop Chrome'] },
        },
    ],
});
