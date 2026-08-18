// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ImportCollectionBundleDialog } from './ImportCollectionBundleDialog';

function renderDialog(onImport = vi.fn()) {
    render(
        <MemoryRouter>
            <ImportCollectionBundleDialog
                onImport={onImport}
                recipesTo='/workspaces/workspace-1/'
                trigger={<button type='button'>Import bundle</button>}
            />
        </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Import bundle' }));

    return { onImport };
}

function chooseFile(name: string) {
    const input = screen.getByLabelText('Collection bundle file') as HTMLInputElement;
    const file = new File(['{}'], name, { type: 'application/zip' });

    fireEvent.change(input, { target: { files: [file] } });

    return file;
}

describe('ImportCollectionBundleDialog', () => {
    afterEach(() => {
        cleanup();
    });

    it('explains what a bundle is before anything is chosen', () => {
        renderDialog();

        expect(screen.getByRole('heading', { name: 'Import a collection bundle' })).toBeDefined();
        expect(screen.getByText(/A bundle is the .zip a collection produces/)).toBeDefined();
        expect(screen.getByRole('link', { name: 'Add recipe' }).getAttribute('href')).toBe('/workspaces/workspace-1/');
    });

    it('keeps import disabled until a bundle is chosen, then hands it over', () => {
        const { onImport } = renderDialog();

        const importButton = screen.getAllByRole('button', { name: 'Import bundle' }).at(-1) as HTMLButtonElement;
        expect(importButton.disabled).toBe(true);

        const file = chooseFile('weeknight-dinners.zip');

        expect(screen.getByText('weeknight-dinners.zip')).toBeDefined();
        expect(importButton.disabled).toBe(false);

        fireEvent.click(importButton);

        expect(onImport).toHaveBeenCalledWith(file);
    });

    it('rejects a file that is not a bundle and says which file to pick', () => {
        const { onImport } = renderDialog();

        chooseFile('sunday-roast.jpg');

        expect(screen.getByText(/is not a bundle/)).toBeDefined();
        expect(screen.queryByText('sunday-roast.jpg')).toBeNull();
        expect(onImport).not.toHaveBeenCalled();
    });
});
