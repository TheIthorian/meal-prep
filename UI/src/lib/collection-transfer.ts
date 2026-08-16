import { unzipSync, zipSync } from 'fflate';
import type { RecipeCollectionExport } from '@/models/meal-prep';

/**
 * Collection export/import bundle handling.
 *
 * A bundle is a single .zip holding `collection-export.json` plus an `images/` folder.
 * Zip is used rather than the File System Access directory picker because that API needs a
 * secure context and Chromium — it is missing on plain-http LAN hosts, Firefox, Safari and mobile.
 * Plain `.json` files (bundles exported before zip support) are still accepted on import.
 */

export const COLLECTION_EXPORT_JSON_NAME = 'collection-export.json';
const IMAGES_PREFIX = 'images/';

export interface CollectionArchive {
    data: RecipeCollectionExport;
    /** Image blobs keyed by their file name inside `images/`. Empty for plain JSON bundles. */
    images: Map<string, Blob>;
}

export function slugifyDownloadName(name: string) {
    return name.replace(/[^\w\s-]/g, '').replace(/\s+/g, '-').slice(0, 80) || 'recipes';
}

export function buildCollectionZip(data: RecipeCollectionExport, images: Map<string, Uint8Array>) {
    const files: Record<string, Uint8Array> = {
        [COLLECTION_EXPORT_JSON_NAME]: new TextEncoder().encode(JSON.stringify(data, null, 2)),
    };
    for (const [fileName, bytes] of images) {
        files[`${IMAGES_PREFIX}${fileName}`] = bytes;
    }
    // Images are already compressed; level 0 keeps large bundles fast.
    return new Blob([zipSync(files, { level: 0 })], { type: 'application/zip' });
}

export async function readCollectionArchive(file: File): Promise<CollectionArchive> {
    const isZip = file.name.toLowerCase().endsWith('.zip') || file.type === 'application/zip';
    if (!isZip) {
        return { data: JSON.parse(await file.text()) as RecipeCollectionExport, images: new Map() };
    }

    const entries = unzipSync(new Uint8Array(await file.arrayBuffer()));
    const jsonEntry = Object.entries(entries).find(([name]) => name.split('/').pop() === COLLECTION_EXPORT_JSON_NAME);
    if (!jsonEntry) throw new Error(`Bundle is missing ${COLLECTION_EXPORT_JSON_NAME}`);

    const images = new Map<string, Blob>();
    for (const [name, bytes] of Object.entries(entries)) {
        if (!name.includes(IMAGES_PREFIX) || name.endsWith('/')) continue;
        const fileName = name.split('/').pop();
        if (fileName) images.set(fileName, new Blob([bytes as BlobPart]));
    }

    return {
        data: JSON.parse(new TextDecoder().decode(jsonEntry[1])) as RecipeCollectionExport,
        images,
    };
}

/** Opens the OS file picker for a collection bundle. Resolves null when the user cancels. */
export async function pickCollectionBundleFile(): Promise<File | null> {
    return await new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.zip,.json,application/zip,application/json';
        input.style.display = 'none';
        // Firefox only fires change events for inputs attached to the document.
        document.body.appendChild(input);
        const cleanup = () => input.remove();
        input.onchange = () => {
            const file = input.files?.[0] ?? null;
            cleanup();
            resolve(file);
        };
        input.oncancel = () => {
            cleanup();
            resolve(null);
        };
        input.click();
    });
}

export function downloadBlob(blob: Blob, fileName: string) {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    // Revoking synchronously can cancel the download in Firefox/Safari.
    setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
