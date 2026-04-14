import type { SaveShoppingListItemRequest, ShoppingList } from '@/models/meal-prep';

const DB_NAME = 'meal-prep-offline';
const DB_VERSION = 1;
const LISTS_STORE = 'shopping_lists';
const PENDING_UPDATES_STORE = 'shopping_item_updates';

interface CachedShoppingListRecord {
    key: string;
    workspaceId: string;
    listId: string;
    list: ShoppingList;
    updatedAtUtc: string;
}

interface PendingShoppingItemUpdateRecord {
    key: string;
    workspaceId: string;
    listId: string;
    itemId: string;
    payload: SaveShoppingListItemRequest;
    updatedAtUtc: string;
}

function listKey(workspaceId: string, listId: string) {
    return `${workspaceId}:${listId}`;
}

function itemUpdateKey(workspaceId: string, listId: string, itemId: string) {
    return `${workspaceId}:${listId}:${itemId}`;
}

function openOfflineDb(): Promise<IDBDatabase | null> {
    if (typeof window === 'undefined' || !('indexedDB' in window)) return Promise.resolve(null);
    return new Promise((resolve, reject) => {
        const request = window.indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(LISTS_STORE)) {
                db.createObjectStore(LISTS_STORE, { keyPath: 'key' });
            }
            if (!db.objectStoreNames.contains(PENDING_UPDATES_STORE)) {
                db.createObjectStore(PENDING_UPDATES_STORE, { keyPath: 'key' });
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

function readOne<T>(db: IDBDatabase, storeName: string, key: string): Promise<T | null> {
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, 'readonly');
        const store = tx.objectStore(storeName);
        const request = store.get(key);
        request.onsuccess = () => resolve((request.result as T | undefined) ?? null);
        request.onerror = () => reject(request.error);
    });
}

function readAll<T>(db: IDBDatabase, storeName: string): Promise<T[]> {
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, 'readonly');
        const store = tx.objectStore(storeName);
        const request = store.getAll();
        request.onsuccess = () => resolve((request.result as T[] | undefined) ?? []);
        request.onerror = () => reject(request.error);
    });
}

function writeOne<T>(db: IDBDatabase, storeName: string, value: T): Promise<void> {
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, 'readwrite');
        const store = tx.objectStore(storeName);
        const request = store.put(value);
        request.onerror = () => reject(request.error);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

function removeOne(db: IDBDatabase, storeName: string, key: string): Promise<void> {
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, 'readwrite');
        const store = tx.objectStore(storeName);
        const request = store.delete(key);
        request.onerror = () => reject(request.error);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function getOfflineShoppingList(workspaceId: string, listId: string): Promise<ShoppingList | null> {
    const db = await openOfflineDb();
    if (!db) return null;
    try {
        const record = await readOne<CachedShoppingListRecord>(db, LISTS_STORE, listKey(workspaceId, listId));
        return record?.list ?? null;
    } finally {
        db.close();
    }
}

export async function saveOfflineShoppingList(
    workspaceId: string,
    listId: string,
    list: ShoppingList,
): Promise<void> {
    const db = await openOfflineDb();
    if (!db) return;
    try {
        await writeOne<CachedShoppingListRecord>(db, LISTS_STORE, {
            key: listKey(workspaceId, listId),
            workspaceId,
            listId,
            list,
            updatedAtUtc: new Date().toISOString(),
        });
    } finally {
        db.close();
    }
}

export async function enqueueOfflineShoppingItemUpdate(
    workspaceId: string,
    listId: string,
    itemId: string,
    payload: SaveShoppingListItemRequest,
): Promise<void> {
    const db = await openOfflineDb();
    if (!db) return;
    try {
        await writeOne<PendingShoppingItemUpdateRecord>(db, PENDING_UPDATES_STORE, {
            key: itemUpdateKey(workspaceId, listId, itemId),
            workspaceId,
            listId,
            itemId,
            payload,
            updatedAtUtc: new Date().toISOString(),
        });
    } finally {
        db.close();
    }
}

export async function getOfflineShoppingItemUpdates(
    workspaceId: string,
    listId: string,
): Promise<PendingShoppingItemUpdateRecord[]> {
    const db = await openOfflineDb();
    if (!db) return [];
    try {
        const records = await readAll<PendingShoppingItemUpdateRecord>(db, PENDING_UPDATES_STORE);
        return records
            .filter(record => record.workspaceId === workspaceId && record.listId === listId)
            .sort((left, right) => left.updatedAtUtc.localeCompare(right.updatedAtUtc));
    } finally {
        db.close();
    }
}

export async function removeOfflineShoppingItemUpdate(
    workspaceId: string,
    listId: string,
    itemId: string,
): Promise<void> {
    const db = await openOfflineDb();
    if (!db) return;
    try {
        await removeOne(db, PENDING_UPDATES_STORE, itemUpdateKey(workspaceId, listId, itemId));
    } finally {
        db.close();
    }
}

export function applyOfflineUpdatesToShoppingList(
    list: ShoppingList,
    updates: Array<{ itemId: string; payload: SaveShoppingListItemRequest }>,
): ShoppingList {
    if (updates.length === 0) return list;
    const byItemId = new Map(updates.map(update => [update.itemId, update.payload]));
    return {
        ...list,
        items: list.items.map(item => {
            const payload = byItemId.get(item.id);
            if (!payload) return item;
            return {
                ...item,
                name: payload.name,
                normalizedIngredientName: payload.normalizedIngredientName ?? null,
                amount: payload.amount ?? null,
                unit: payload.unit ?? null,
                isApproximate: payload.isApproximate,
                isChecked: payload.isChecked,
                isManual: payload.isManual,
                category: payload.category ?? null,
                note: payload.note ?? null,
                displayText: payload.displayText,
                sourceNames: payload.sourceNames ?? [],
            };
        }),
    };
}
