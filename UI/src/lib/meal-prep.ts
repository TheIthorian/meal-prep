import type {
    MealPlanEntry,
    Recipe,
    RecipeIngredient,
    RecipeListItem,
    RecipeNutrition,
    SaveRecipeIngredientRequest,
    SaveRecipeRequest,
    ShoppingListItem,
    ShoppingListListItem,
} from '@/models/meal-prep';

/** Human-readable label for canonical kebab-case recipe tags (e.g. light-lunch → Light Lunch). */
export function formatRecipeTagLabel(tag: string) {
    return tag
        .split('-')
        .map(part => (part.length > 0 ? part.charAt(0).toUpperCase() + part.slice(1) : part))
        .join(' ');
}

export function formatAmount(amount?: number | null) {
    if (amount === null || amount === undefined || Number.isNaN(amount)) return '';
    return Number(amount.toFixed(2)).toString();
}

export function buildIngredientDisplay(ingredient: {
    amount?: number | null;
    unit?: string | null;
    name: string;
    preparationNote?: string | null;
}) {
    const amountPart =
        ingredient.amount !== null && ingredient.amount !== undefined ? formatAmount(ingredient.amount) : '';
    const baseText = [amountPart, ingredient.unit?.trim(), ingredient.name.trim()].filter(Boolean).join(' ');
    return ingredient.preparationNote?.trim() ? `${baseText}, ${ingredient.preparationNote.trim()}` : baseText;
}

/** Label for ingredient lists: name plus optional preparation note (e.g. "Tomatoes, diced"). */
export function getIngredientListLabel(ingredient: Pick<RecipeIngredient, 'name' | 'preparationNote'>): string {
    const prep = ingredient.preparationNote?.trim();
    const name = ingredient.name.trim();
    return prep ? `${name}, ${prep}` : name;
}

/** Scaled amount + unit for list display, or null when there is no numeric amount. */
export function getIngredientListAmountText(ingredient: Pick<RecipeIngredient, 'amount' | 'unit'>): string | null {
    const { amount, unit } = ingredient;
    if (amount === null || amount === undefined || Number.isNaN(amount)) {
        return null;
    }
    const amountPart = formatAmount(amount);
    const u = unit?.trim();
    return u ? `${amountPart} ${u}` : amountPart;
}

export function scaleRecipeIngredients(
    ingredients: RecipeIngredient[],
    originalServings: number,
    targetServings: number,
): RecipeIngredient[] {
    if (!originalServings || originalServings <= 0 || Math.abs(originalServings - targetServings) < 1e-6) {
        return ingredients;
    }

    const ratio = targetServings / originalServings;
    return ingredients.map(ingredient => {
        const amount =
            ingredient.amount === null || ingredient.amount === undefined
                ? ingredient.amount
                : Number((ingredient.amount * ratio).toFixed(3));
        const displayText = buildIngredientDisplay({
            amount,
            unit: ingredient.unit,
            name: ingredient.name,
            preparationNote: ingredient.preparationNote,
        });

        return {
            ...ingredient,
            amount,
            displayText,
        };
    });
}

export function toSaveRecipeIngredient(ingredient: RecipeIngredient): SaveRecipeIngredientRequest {
    return {
        name: ingredient.name,
        normalizedIngredientName: ingredient.normalizedIngredientName,
        amount: ingredient.amount,
        unit: ingredient.unit,
        preparationNote: ingredient.preparationNote,
        section: ingredient.section,
        displayText: ingredient.displayText,
    };
}

export function createEmptyRecipe(workspaceId: string): Recipe {
    return {
        id: '',
        workspaceId,
        title: '',
        description: '',
        servings: 4,
        sourceUrl: '',
        notes: '',
        prepMinutes: null,
        cookMinutes: null,
        isArchived: false,
        tags: [],
        hasImage: false,
        isFavorite: false,
        importImageUrl: null,
        ingredients: [
            {
                id: crypto.randomUUID(),
                sortOrder: 0,
                name: '',
                displayText: '',
                amount: null,
                unit: '',
                preparationNote: '',
                section: '',
            },
        ],
        steps: [{ id: crypto.randomUUID(), sortOrder: 0, instruction: '', timerSeconds: null }],
        nutrition: null,
    };
}

export function getNutrientAmount(nutrition: RecipeNutrition | null | undefined, nutrientType: string): number | null {
    const found = nutrition?.nutrients.find(
        nutrient => nutrient.nutrientType.toLowerCase() === nutrientType.toLowerCase(),
    );
    return found ? Number(found.amount) : null;
}

export function setNutrientAmount(recipe: Recipe, nutrientType: string, amount: number | null): Recipe {
    const base =
        recipe.nutrition?.nutrients.filter(
            nutrient => nutrient.nutrientType.toLowerCase() !== nutrientType.toLowerCase(),
        ) ?? [];

    const nutrients =
        amount === null || Number.isNaN(amount) ? base : [...base, { id: crypto.randomUUID(), nutrientType, amount }];

    if (
        nutrients.length === 0 &&
        (recipe.nutrition?.servingBasis === null || recipe.nutrition?.servingBasis === undefined)
    ) {
        return { ...recipe, nutrition: null };
    }

    return {
        ...recipe,
        nutrition: {
            servingBasis: recipe.nutrition?.servingBasis ?? null,
            nutrients,
        },
    };
}

export function setServingBasis(recipe: Recipe, servingBasis: number | null): Recipe {
    const nutrients = recipe.nutrition?.nutrients ?? [];
    if (nutrients.length === 0 && (servingBasis === null || servingBasis === undefined)) {
        return { ...recipe, nutrition: null };
    }
    return {
        ...recipe,
        nutrition: {
            servingBasis,
            nutrients,
        },
    };
}

export function toSaveRecipeRequest(recipe: Recipe): SaveRecipeRequest {
    return {
        title: recipe.title,
        description: recipe.description,
        servings: recipe.servings,
        sourceUrl: recipe.sourceUrl,
        notes: recipe.notes,
        prepMinutes: recipe.prepMinutes,
        cookMinutes: recipe.cookMinutes,
        isArchived: recipe.isArchived,
        tags: recipe.tags,
        ingredients: recipe.ingredients.map(ingredient => toSaveRecipeIngredient(ingredient)),
        steps: recipe.steps.map(step => ({
            instruction: step.instruction,
            timerSeconds: step.timerSeconds,
        })),
        nutrition:
            recipe.nutrition && recipe.nutrition.nutrients.length > 0
                ? {
                      servingBasis: recipe.nutrition.servingBasis,
                      nutrients: recipe.nutrition.nutrients.map(n => ({
                          nutrientType: n.nutrientType,
                          amount: n.amount,
                      })),
                  }
                : null,
        importImageUrl: recipe.importImageUrl ?? undefined,
    };
}

export function getRecipeDurationLabel(recipe: Pick<Recipe, 'prepMinutes' | 'cookMinutes'>) {
    const prep = recipe.prepMinutes ? `${recipe.prepMinutes}m prep` : null;
    const cook = recipe.cookMinutes ? `${recipe.cookMinutes}m cook` : null;
    return [prep, cook].filter(Boolean).join(' • ');
}

export function getShoppingListProgress(list: Pick<ShoppingListListItem, 'checkedItemCount' | 'totalItemCount'>) {
    if (!list.totalItemCount) return 0;
    return Math.round((list.checkedItemCount / list.totalItemCount) * 100);
}

export function startOfWeek(date = new Date()) {
    const copy = new Date(date);
    const day = copy.getDay();
    const diff = day === 0 ? -6 : 1 - day;
    copy.setDate(copy.getDate() + diff);
    copy.setHours(0, 0, 0, 0);
    return copy;
}

export function addDays(date: Date, days: number) {
    const copy = new Date(date);
    copy.setDate(copy.getDate() + days);
    return copy;
}

export function toDateInputValue(date: Date) {
    return date.toISOString().slice(0, 10);
}

export function formatDateLabel(value: string) {
    return new Date(`${value}T12:00:00`).toLocaleDateString(undefined, {
        weekday: 'short',
        month: 'short',
        day: 'numeric',
    });
}

export function mealTypeLabel(mealType: string) {
    return mealType.charAt(0).toUpperCase() + mealType.slice(1);
}

export function groupEntriesByDate(entries: MealPlanEntry[]) {
    return entries.reduce<Record<string, MealPlanEntry[]>>((groups, entry) => {
        groups[entry.plannedDate] ??= [];
        groups[entry.plannedDate].push(entry);
        return groups;
    }, {});
}

export function getTodaysEntries(entries: MealPlanEntry[]) {
    const today = toDateInputValue(new Date());
    return entries.filter(entry => entry.plannedDate === today);
}

export function searchRecipes(recipes: RecipeListItem[], term: string) {
    const normalized = term.trim().toLowerCase();
    if (!normalized) return recipes;

    return recipes.filter(recipe => {
        const haystack = [recipe.title, recipe.description ?? '', recipe.tags.join(' ')].join(' ').toLowerCase();
        return haystack.includes(normalized);
    });
}

export function toggleShoppingItem(items: ShoppingListItem[], itemId: string) {
    return items.map(item => (item.id === itemId ? { ...item, isChecked: !item.isChecked } : item));
}

/** Returns an http(s) href safe to use in a link, or null if the value is missing or not a web URL. */
export function safeHttpUrlHref(raw: string | null | undefined): string | null {
    if (raw === null || raw === undefined) return null;
    const trimmed = raw.trim();
    if (!trimmed) return null;
    try {
        const u = new URL(trimmed);
        return u.protocol === 'http:' || u.protocol === 'https:' ? u.href : null;
    } catch {
        return null;
    }
}

/** Full URL for authenticated GET of the recipe cover image (uses session cookies). */
export function recipeImageRequestUrl(workspaceId: string, recipeId: string) {
    const path = `/api/v1/workspaces/${workspaceId}/recipes/${recipeId}/image`;
    const base = import.meta.env.VITE_API_BASE_URL || (import.meta.env.DEV ? '' : 'http://192.168.1.98:5001');
    return `${base}${path}`;
}
