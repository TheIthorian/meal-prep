import { expect, test, type Page } from '@playwright/test';

const SHARE_TOKEN = 'e2e-share-token';
const JOB_ID = '11111111-1111-1111-1111-111111111111';
const COLLECTION_ID = '22222222-2222-2222-2222-222222222222';

function uniqueEmail() {
    const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
    return `meal-prep-collection-import-${nonce}@example.com`;
}

interface ImportJobOptions {
    status: 'pending' | 'running' | 'completed' | 'completedWithErrors' | 'failed';
    total: number;
    imported: number;
    failedTitles?: string[];
    targetCollectionId?: string | null;
}

function importJob(workspaceId: string, options: ImportJobOptions) {
    const failures = (options.failedTitles ?? []).map((recipeTitle, index) => ({
        sourceRecipeId: `33333333-3333-3333-3333-00000000000${index}`,
        recipeTitle,
        errorMessage: 'The recipe image could not be copied.',
    }));

    return {
        id: JOB_ID,
        workspaceId,
        status: options.status,
        shareToken: SHARE_TOKEN,
        sourceCollectionName: 'Weeknight Favourites',
        totalRecipes: options.total,
        processedRecipes: options.imported + failures.length,
        importedRecipes: options.imported,
        failedRecipes: failures.length,
        targetCollectionId: options.targetCollectionId ?? null,
        createdAt: new Date().toISOString(),
        completedAt: options.status === 'running' || options.status === 'pending' ? null : new Date().toISOString(),
        errorMessage: null,
        failures,
    };
}

async function mockSharePreview(page: Page) {
    await page.route(`**/api/v1/recipe-collection-share/${SHARE_TOKEN}`, route =>
        route.fulfill({
            json: {
                collectionName: 'Weeknight Favourites',
                description: 'Shared for import',
                ownerWorkspaceName: 'Friend Workspace',
                recipeCount: 3,
            },
        }),
    );
}

async function readWorkspaceId(page: Page) {
    await page.goto('/settings');
    await expect(page).toHaveURL(/\/settings/);

    const response = await page.request.get('/api/v1/me');
    expect(response.ok()).toBeTruthy();

    const user = (await response.json()) as { workspaces: { workspaceId: string }[] };
    return user.workspaces[0].workspaceId;
}

test.describe('Shared collection import progress', () => {
    let workspaceId = '';

    test.beforeEach(async ({ page }) => {
        const email = uniqueEmail();
        const password = `S-${Date.now()}-Strong!Pass123`;

        await page.goto('/register');
        await page.getByLabel('Email').fill(email);
        await page.getByLabel('Display Name (Optional)').fill('E2E Import User');
        await page.getByLabel('Password', { exact: true }).fill(password);
        await page.getByLabel('Confirm Password').fill(password);
        await page.getByRole('button', { name: 'Create Account' }).click();

        await expect(page).toHaveURL(/\/login$/, { timeout: 45_000 });

        await page.getByLabel('Email').fill(email);
        await page.getByLabel('Password', { exact: true }).fill(password);
        await page.getByRole('button', { name: 'Sign In' }).click();

        await expect(page).toHaveURL(/(\/settings$)|(\/workspaces\/[^/]+\/?$)/, { timeout: 45_000 });

        workspaceId = await readWorkspaceId(page);
        await mockSharePreview(page);
    });

    test('counts recipes as they import and offers a retry for the ones that failed', async ({ page }) => {
        await page.route('**/recipe-collection-import-jobs?*', route => route.fulfill({ json: [] }));
        await page.route(`**/recipe-collection-import/${SHARE_TOKEN}/jobs`, route =>
            route.fulfill({
                status: 202,
                json: importJob(workspaceId, { status: 'pending', total: 3, imported: 0 }),
            }),
        );

        let poll = 0;
        await page.route(`**/recipe-collection-import-jobs/${JOB_ID}`, route => {
            poll += 1;

            const body =
                poll === 1
                    ? importJob(workspaceId, {
                          status: 'running',
                          total: 3,
                          imported: 1,
                          targetCollectionId: COLLECTION_ID,
                      })
                    : importJob(workspaceId, {
                          status: 'completedWithErrors',
                          total: 3,
                          imported: 2,
                          failedTitles: ['Miso Ramen'],
                          targetCollectionId: COLLECTION_ID,
                      });

            return route.fulfill({ json: body });
        });

        await page.goto(`/share/recipe-collections/${SHARE_TOKEN}`);
        await expect(page.getByRole('heading', { name: 'Import shared collection' })).toBeVisible();

        await page.getByRole('button', { name: 'Import collection' }).click();

        const progress = page.getByTestId('import-progress');
        await expect(progress).toContainText('1 of 3');
        await expect(progress).toContainText('Importing recipes');

        await expect(progress).toContainText('Imported with errors');
        await expect(page.getByTestId('import-failures')).toContainText('Miso Ramen');
        await expect(page.getByRole('button', { name: 'Retry failed recipes' })).toBeVisible();
        await expect(page.getByRole('button', { name: 'View imported collection' })).toBeVisible();
    });

    test('picks a running import back up after the page is reloaded', async ({ page }) => {
        await page.route('**/recipe-collection-import-jobs?*', route =>
            route.fulfill({
                json: [
                    importJob(workspaceId, {
                        status: 'running',
                        total: 3,
                        imported: 2,
                        targetCollectionId: COLLECTION_ID,
                    }),
                ],
            }),
        );
        await page.route(`**/recipe-collection-import-jobs/${JOB_ID}`, route =>
            route.fulfill({
                json: importJob(workspaceId, {
                    status: 'running',
                    total: 3,
                    imported: 2,
                    targetCollectionId: COLLECTION_ID,
                }),
            }),
        );

        await page.goto(`/share/recipe-collections/${SHARE_TOKEN}`);

        const progress = page.getByTestId('import-progress');
        await expect(progress).toContainText('2 of 3');
        await expect(progress).toContainText('This keeps running if you close the page');
        await expect(page.getByRole('button', { name: 'Import collection' })).toHaveCount(0);
    });
});
