import { expect, test } from '@playwright/test';
import { expectVisualSimilarity } from './support/screenshot-similarity.js';

function uniqueEmail() {
    const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
    return `myapp-settings-${nonce}@example.com`;
}

test.describe('Settings flows', () => {
    let email = '';
    let password = '';

    test.beforeEach(async ({ page }) => {
        email = uniqueEmail();
        password = `S-${Date.now()}-Strong!Pass123`;

        await page.goto('/register');
        await page.getByLabel('Email').fill(email);
        await page.getByLabel('Display Name (Optional)').fill('E2E Settings User');
        await page.getByLabel('Password', { exact: true }).fill(password);
        await page.getByLabel('Confirm Password').fill(password);
        await page.getByRole('button', { name: 'Create Account' }).click();

        await expect(page).toHaveURL(/\/login$/, { timeout: 45_000 });

        await page.getByLabel('Email').fill(email);
        await page.getByLabel('Password', { exact: true }).fill(password);
        await page.getByRole('button', { name: 'Sign In' }).click();

        await expect(page).toHaveURL(/(\/settings$)|(\/workspaces\/[^/]+$)/, {
            timeout: 45_000,
        });

        await page.goto('/settings');
        await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible();
    });

    test('validates profile tab functionality', async ({ page }) => {
        await expect(page.getByRole('tab', { name: 'Profile' })).toHaveAttribute('data-state', 'active');

        await page.getByLabel('Display Name').fill('Updated Settings User');
        await page.getByRole('button', { name: 'Save Changes' }).click();

        await expect(page.getByText('Profile updated', { exact: true })).toBeVisible();

        await expectVisualSimilarity(page.locator('.animate-fade-in'), {
            name: 'settings-profile-tab',
            minSimilarity: 0.98,
        });
    });

    test('validates preferences tab functionality', async ({ page }) => {
        await page.getByRole('tab', { name: 'Preferences' }).click();
        await expect(page.getByRole('tab', { name: 'Preferences' })).toHaveAttribute('data-state', 'active');

        await page.locator('#currency').click();
        await page.getByRole('option', { name: 'EUR (€)' }).click();

        await page.locator('#dateFormat').click();
        await page.getByRole('option', { name: 'YYYY-MM-DD' }).click();

        await page.getByRole('button', { name: 'Save Preferences' }).click();
        await expect(page.getByText('Preferences saved', { exact: true })).toBeVisible();

        // Refresh page and verify visually or by checking the local storage / select value
        await page.reload();
        await page.getByRole('tab', { name: 'Preferences' }).click();
        await expect(page.locator('#currency')).toHaveText('EUR (€)');
        await expect(page.locator('#dateFormat')).toHaveText('YYYY-MM-DD');

        await expectVisualSimilarity(page.locator('.animate-fade-in'), {
            name: 'settings-preferences-tab',
            minSimilarity: 0.98,
        });
    });

    test('validates workspaces tab functionality', async ({ page }) => {
        await page.getByRole('tab', { name: 'Workspaces' }).click();
        await expect(page.getByRole('tab', { name: 'Workspaces' })).toHaveAttribute('data-state', 'active');

        await expect(page.getByRole('heading', { name: 'My Workspaces' })).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Workspace Members' })).toBeVisible();

        await page.getByRole('button', { name: 'Create Workspace' }).click();
        await page.getByLabel('Workspace Name').fill('E2E Test Workspace');
        await page.getByRole('dialog').getByRole('button', { name: 'Create Workspace' }).click();

        await expect(page.getByText('Workspace created', { exact: true })).toBeVisible();
        await expect(page.getByText('E2E Test Workspace', { exact: true })).toBeVisible();
        await expect(page.getByRole('dialog')).not.toBeVisible();

        await expectVisualSimilarity(page.locator('.animate-fade-in'), {
            name: 'settings-workspaces-tab',
            minSimilarity: 0.98,
        });
    });
});
