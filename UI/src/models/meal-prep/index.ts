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
    isFavorite: boolean;
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
    isFavorite: boolean;
    collections?: RecipeCollectionMembership[];
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

export interface RecipeTagUsageItem {
    tag: string;
    recipeCount: number;
}

export interface RecipeTagUsageListResponse {
    items: RecipeTagUsageItem[];
}

export interface BulkRemoveRecipeTagsResponse {
    recipesUpdated: number;
    tagsProcessed: string[];
}

export interface RecipeCollectionListItem {
    id: string;
    name: string;
    description?: string | null;
    recipeCount: number;
    ownerWorkspaceId: string;
    isOwnedByViewerWorkspace: boolean;
}

export interface RecipeCollectionMembership {
    collectionId: string;
    collectionName: string;
    ownerWorkspaceId: string;
    isOwnedByViewerWorkspace: boolean;
}

export interface RecipeCollectionSharedWorkspace {
    workspaceId: string;
    workspaceName: string;
}

export interface RecipeCollectionDetail {
    id: string;
    name: string;
    description?: string | null;
    ownerWorkspaceId: string;
    canEdit: boolean;
    recipes: RecipeListItem[];
    sharedWithWorkspaces: RecipeCollectionSharedWorkspace[];
}

export interface CreateRecipeCollectionRequest {
    name: string;
    description?: string | null;
}

export interface PatchRecipeCollectionRequest {
    name: string;
    description?: string | null;
}

export interface RecipeCollectionExportRecipe {
    recipeId: string;
    title: string;
    imageFileName?: string | null;
    payload: SaveRecipeRequest;
}

export interface RecipeCollectionExport {
    collectionName: string;
    description?: string | null;
    exportedAtUtc: string;
    recipes: RecipeCollectionExportRecipe[];
}

export interface RecipeCollectionShareLink {
    shareToken: string;
    importPath: string;
    createdAtUtc: string;
}

export interface RecipeCollectionShareLinkPreview {
    collectionName: string;
    description?: string | null;
    ownerWorkspaceName: string;
    recipeCount: number;
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

export interface NextMeal {
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
    completedAtUtc?: string | null;
}

export interface SaveNextMealRequest {
    recipeId: string;
    plannedDate: string;
    mealType: string;
    targetServings?: number | null;
    notes?: string | null;
    status: string;
    completedAtUtc?: string | null;
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
    nextMealId?: string | null;
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
    nextMealIds: string[];
}

// Backward-compat aliases while UI migrates off "meal plan" naming.
export type MealPlanEntry = NextMeal;
export type SaveMealPlanEntryRequest = SaveNextMealRequest;

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
