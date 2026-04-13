export interface RecipeListItem {
    id: string;
    title: string;
    description?: string | null;
    servings: number;
    isArchived: boolean;
    tags: string[];
    sourceUrl?: string | null;
    ingredientCount: number;
    stepCount: number;
    hasImage: boolean;
}

export interface RecipeIngredient {
    id: string;
    sortOrder: number;
    name: string;
    normalizedIngredientName?: string | null;
    amount?: number | null;
    unit?: string | null;
    preparationNote?: string | null;
    section?: string | null;
    displayText: string;
}

export interface RecipeStep {
    id: string;
    sortOrder: number;
    instruction: string;
    timerSeconds?: number | null;
}

/** Matches API nutrient type strings (see RecipeNutrientTypes on the server). */
export type RecipeNutrientType = 'calories' | 'protein' | 'carbohydrate' | 'fat' | 'fiber' | 'sugar' | 'sodium';

export interface RecipeNutrient {
    id: string;
    nutrientType: string;
    amount: number;
}

export interface RecipeNutrition {
    servingBasis?: number | null;
    nutrients: RecipeNutrient[];
}

export interface Recipe {
    id: string;
    workspaceId: string;
    title: string;
    description?: string | null;
    servings: number;
    sourceUrl?: string | null;
    notes?: string | null;
    prepMinutes?: number | null;
    cookMinutes?: number | null;
    isArchived: boolean;
    tags: string[];
    hasImage: boolean;
    /** Set when saving after URL import; server downloads and stores the image once. */
    importImageUrl?: string | null;
    ingredients: RecipeIngredient[];
    steps: RecipeStep[];
    nutrition?: RecipeNutrition | null;
}

export interface SaveRecipeIngredientRequest {
    name: string;
    normalizedIngredientName?: string | null;
    amount?: number | null;
    unit?: string | null;
    preparationNote?: string | null;
    section?: string | null;
    displayText: string;
}

export interface SaveRecipeStepRequest {
    instruction: string;
    timerSeconds?: number | null;
}

export interface SaveRecipeNutrientRequest {
    nutrientType: string;
    amount: number;
}

export interface SaveRecipeNutritionRequest {
    servingBasis?: number | null;
    nutrients: SaveRecipeNutrientRequest[];
}

export interface SaveRecipeRequest {
    title: string;
    description?: string | null;
    servings: number;
    sourceUrl?: string | null;
    notes?: string | null;
    prepMinutes?: number | null;
    cookMinutes?: number | null;
    isArchived: boolean;
    tags: string[];
    ingredients: SaveRecipeIngredientRequest[];
    steps: SaveRecipeStepRequest[];
    nutrition?: SaveRecipeNutritionRequest | null;
    importImageUrl?: string | null;
}

export interface RecipeTagListResponse {
    tags: string[];
}

export interface SuggestRecipeTagsRequest {
    title: string;
    description?: string | null;
    ingredientNames?: string[];
    stepInstructions?: string[];
}

export interface SuggestRecipeTagsResponse {
    tags: string[];
}

export interface RecipeImportPreview {
    title: string;
    description?: string | null;
    servings: number;
    sourceUrl: string;
    prepMinutes?: number | null;
    cookMinutes?: number | null;
    tags: string[];
    ingredients: RecipeIngredient[];
    steps: RecipeStep[];
    nutrition?: RecipeNutrition | null;
    imageUrl?: string | null;
}

export interface MealPlanEntry {
    id: string;
    workspaceId: string;
    recipeId: string;
    recipeTitle: string;
    recipeDescription?: string | null;
    plannedDate: string;
    mealType: string;
    targetServings?: number | null;
    notes?: string | null;
    status: string;
}

export interface SaveMealPlanEntryRequest {
    recipeId: string;
    plannedDate: string;
    mealType: string;
    targetServings?: number | null;
    notes?: string | null;
    status: string;
}

export interface ShoppingListListItem {
    id: string;
    name: string;
    notes?: string | null;
    generatedAt?: string | null;
    totalItemCount: number;
    checkedItemCount: number;
}

export interface ShoppingListItem {
    id: string;
    sortOrder: number;
    name: string;
    normalizedIngredientName?: string | null;
    amount?: number | null;
    unit?: string | null;
    isApproximate: boolean;
    isChecked: boolean;
    isManual: boolean;
    category?: string | null;
    note?: string | null;
    displayText: string;
    /** Recipe or meal-plan labels this line was consolidated from (e.g. when one ingredient appears in multiple sources). */
    sourceNames: string[];
}

export interface ShoppingListSource {
    id: string;
    recipeId?: string | null;
    mealPlanEntryId?: string | null;
    sourceName: string;
}

export interface ShoppingList {
    id: string;
    workspaceId: string;
    name: string;
    notes?: string | null;
    generatedAt?: string | null;
    items: ShoppingListItem[];
    sources: ShoppingListSource[];
}

export interface GenerateShoppingListRequest {
    name: string;
    notes?: string | null;
    recipeIds: string[];
    mealPlanEntryIds: string[];
}

export interface SaveShoppingListRequest {
    name: string;
    notes?: string | null;
}

export interface SaveShoppingListItemRequest {
    name: string;
    normalizedIngredientName?: string | null;
    amount?: number | null;
    unit?: string | null;
    isApproximate: boolean;
    isChecked: boolean;
    isManual: boolean;
    category?: string | null;
    note?: string | null;
    displayText: string;
    sourceNames?: string[] | null;
}
