import { expect, Page, test } from '@playwright/test';

function uniqueEmail() {
    const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
    return `meal-prep-logout-${nonce}@example.com`;
}

async function registerAndSignIn(page: Page) {
    const email = uniqueEmail();
    const password = `S-${Date.now()}-Strong!Pass123`;

    await page.goto('/register');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Display Name (Optional)').fill('E2E Logout User');
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByLabel('Confirm Password').fill(password);
    await page.getByRole('button', { name: 'Create Account' }).click();

    await expect(page).toHaveURL(/\/login$/, { timeout: 45_000 });

    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Sign In' }).click();

    await expect(page).toHaveURL(/(\/settings$)|(\/workspaces\/[^/]+$)/, { timeout: 45_000 });
}

test.describe('Logout', () => {
    test('signs out from the sidebar on desktop', async ({ page }) => {
        await registerAndSignIn(page);

        // Visible in the sidebar without opening any menu.
        const logout = page.locator('[data-sidebar="sidebar"]').getByRole('button', { name: 'Log out' });
        await expect(logout).toBeVisible();
        await logout.click();

        await expect(page).toHaveURL(/\/login$/, { timeout: 45_000 });

        // The session is gone, so a protected page bounces back to login.
        await page.goto('/settings');
        await expect(page).toHaveURL(/\/login/, { timeout: 45_000 });
    });

    test('signs out from the header on mobile', async ({ page }) => {
        await page.setViewportSize({ width: 390, height: 844 });
        await registerAndSignIn(page);

        // The sidebar is off-canvas on mobile, so the header carries the action.
        const logout = page.locator('header').getByRole('button', { name: 'Log out' });
        await expect(logout).toBeVisible();
        await logout.click();

        await expect(page).toHaveURL(/\/login$/, { timeout: 45_000 });
    });
});
