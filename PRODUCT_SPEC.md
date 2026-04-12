# Meal Prep Product Specification

## Purpose

Meal Prep is a workspace-based web application for collecting recipes, planning meals, and turning plans into usable shopping workflows.

The app should let a user or household:

- store and organize recipes
- import recipes from web URLs
- plan what to cook for the rest of the week
- generate shopping lists from planned meals
- use a dedicated cooking mode while preparing food
- use a dedicated shopping mode while buying ingredients
- manage dynamic ingredient quantities with unit-aware conversion
- attach nutrition information to recipes
- keep all user-created resources linked to workspaces
- integrate with online supermarkets to transfer shopping lists

This document defines the intended product scope and should act as a baseline for backend, frontend, and API design.

## Product Goals

- Make recipe capture fast enough that users prefer saving recipes here instead of bookmarks or notes.
- Reduce the effort required to turn recipes into an actionable shopping list.
- Make in-kitchen and in-store usage meaningfully better than the default desktop UI.
- Support shared planning for a household through existing workspace concepts in the repo.
- Build a foundation for retailer integrations without making core meal planning depend on them.

## Non-Goals

- Full nutrition coaching or calorie tracking in the initial version.
- Open-ended pantry or inventory management as a first-class system.
- General grocery delivery orchestration across every retailer from day one.
- Social recipe publishing or public marketplace features in the initial version.

## Primary Users

### Household planner

The person who decides what to cook this week, collects recipes, and creates the shopping list.

### Household collaborator

A second member of the same workspace who can view plans, cook from recipes, and complete shopping tasks.

### High-frequency cook

A user who cooks directly from a phone or tablet and needs a reliable, low-friction cooking experience.

## Core User Outcomes

- I can save a recipe once and reuse it.
- I can import a recipe from a URL instead of copying it manually.
- I can choose recipes for upcoming days and see a clear week plan.
- I can automatically generate a shopping list from those planned meals.
- I can scale servings and have ingredient quantities update correctly.
- I can store nutrition information on recipes when it is useful.
- I can shop from a phone with large controls and progress feedback.
- I can cook from a phone or tablet without scrolling through clutter.
- My data is separated by workspace, so a household or team can collaborate safely.

## Product Principles

- Workspace-first: all recipes, plans, shopping lists, and integrations belong to a workspace.
- Mobile-critical: cooking mode and shopping mode are mobile-first experiences, not resized desktop pages.
- Structured where it matters: ingredients, units, and quantities should be modeled, not left as plain text where avoidable.
- Graceful fallback: retailer integrations are helpful accelerators, but the core planning and list workflow must work without them.
- Traceable automation: whenever the system imports or converts data, the user should be able to review and correct it.

## Functional Scope

### 1. Recipe Library

Users can create, view, edit, archive, and organize recipes inside a workspace.

### Requirements

- A recipe belongs to exactly one workspace.
- A recipe has a title, optional description, servings, ingredients, steps, optional prep/cook time, optional notes, and optional source URL.
- A recipe can include optional nutrition information.
- A recipe supports both structured ingredients and a user-visible display form.
- A recipe can be tagged or categorized, for example: breakfast, dinner, vegetarian, quick meals.
- A recipe can be archived without hard deletion from normal views.
- Recipes are searchable by title, tags, and ingredient names.

### Nutrition information requirements

- Nutrition data on a recipe is optional and editable by the user.
- Nutrition data should support a serving basis so the values can be understood in relation to recipe servings.
- The initial nutrition model should support common fields such as:
  - calories
  - protein
  - carbohydrates
  - fat
  - fiber
  - sugar
  - sodium
- Imported nutrition information should be reviewable before save when extracted from a source page.
- Missing or partial nutrition data must not block recipe creation or import.

### Structured ingredient requirements

- Each ingredient row should support:
  - ingredient name
  - optional normalized ingredient reference
  - amount
  - unit
  - optional preparation note
  - optional section, such as sauce or topping
- Ingredient rows must support decimal amounts and fractional display where useful.
- Ingredients should preserve the original imported wording when parsing confidence is low.

### 2. Recipe Import From Web URL

Users can provide a recipe URL and the app will attempt to extract a recipe into the workspace library.

### Requirements

- User submits a URL within a workspace context.
- The system fetches the source page and attempts structured extraction.
- The imported recipe is shown in a review state before final save.
- The user can edit title, servings, ingredients, units, and steps before confirming.
- The source URL is retained on the saved recipe.
- Import failures should return a clear reason and offer manual recipe creation as fallback.

### Import parsing expectations

- Prefer structured recipe metadata when present on the page.
- Fall back to heuristic extraction when structured metadata is missing.
- Preserve source attribution fields where available.
- Keep a raw import snapshot or source payload for debugging if operationally appropriate.

### 3. Weekly Meal Planning

Users can plan what to cook for the rest of the week by assigning recipes to days and meal slots.

### Requirements

- Meal plans belong to a workspace.
- Users can add one or more recipes to a date.
- A plan entry can optionally store:
  - meal type, such as breakfast, lunch, dinner
  - target servings
  - notes
  - completion status
- The week view should make it easy to plan the remainder of the current week.
- Users can copy or move a planned recipe to another day.
- Users can remove a planned recipe without deleting the recipe itself.

### UX expectations

- Fast add from existing recipes.
- Clear empty states for unplanned days.
- A condensed weekly overview and a per-day detail view.
- Mobile support for reviewing the current day’s plan.

### 4. Shopping List Generation

Users can generate a shopping list from selected planned meals or selected recipes.

### Requirements

- A shopping list belongs to a workspace.
- A shopping list can be generated from:
  - one or more planned meals
  - one or more recipes directly
- Ingredient quantities from multiple sources should merge when compatible.
- Duplicate ingredients should be consolidated where confidence is high.
- The generated list should preserve a reference back to the originating recipes or meal plan entries.
- Users can manually add, edit, check off, and remove list items after generation.
- Users can regenerate or refresh a list while preserving manual adjustments through a defined conflict strategy.

### Consolidation rules

- Identical normalized ingredients with compatible units should merge into one item.
- Incompatible units should either convert automatically or stay as separate entries with clear explanation.
- Low-confidence matches should not merge silently.

### 5. Dynamic Ingredient Quantities And Unit Conversion

Recipes and shopping lists should support unit-aware scaling and conversion.

### Requirements

- Users can change target servings for a recipe or meal plan entry.
- Ingredient amounts recalculate based on serving changes.
- The system supports conversion between compatible units, for example:
  - g, kg, oz
  - ml, l, floz
- The system should distinguish between:
  - exact convertible units
  - approximate culinary conversions
  - non-convertible units, such as "1 onion" or "to taste"
- The UI should make it clear when a value is exact versus approximate.

### Domain rules

- The system needs a canonical measurement model for mass, volume, and count.
- Free-text units must still be supported for imported recipes.
- Unsupported or ambiguous units should remain editable and should not block saving a recipe.

### 6. Cooking Mode

Cooking mode is a focused recipe experience optimized for active cooking on phone or tablet.

### Requirements

- Large touch targets for step navigation.
- Clear ingredient checklist or ingredient reference alongside steps.
- Screen should stay focused on the current step with minimal surrounding chrome.
- Users can scale servings inside cooking mode.
- Steps should support timers where timing text is available or manually added.
- Ingredients or steps marked as completed should remain visible but clearly completed.
- The view should work well in portrait mobile layouts.

### UX expectations

- Minimal need for precision tapping.
- Strong readability from arm’s length.
- Prevent accidental loss of place when the screen refreshes or navigation changes.
- Optional keep-screen-awake support can be considered later, but is not required for first release.

### 7. Shopping Mode

Shopping mode is a focused mobile list experience optimized for walking through a store.

### Requirements

- Large tap targets and checkbox areas.
- High-contrast, low-clutter layout.
- List progress indicator, for example items remaining.
- Ability to sort or group items by aisle/category where data is available.
- Offline-tolerant interaction is preferred for checking items off during poor connectivity.
- Manual item editing must remain easy on mobile.

### UX expectations

- One-handed use on a phone.
- Rapid check-off flow with minimal accidental taps.
- A quick way to hide completed items.
- A persistent summary of what remains to buy.

### 8. Workspace Scoping

The repo already uses workspaces as a core boundary. The app specification must preserve that model across all new resources.

### Requirements

- Recipes, recipe imports, meal plans, shopping lists, retailer connections, and related settings are workspace-owned resources.
- Backend queries for workspace-owned data must remain filtered to the current user’s accessible workspaces.
- Sharing happens through workspace membership, not ad hoc per-record sharing.
- Deleting a workspace removes or archives all associated product data according to the existing retention approach.
- Analytics should use stable identifiers such as `workspaceId` and avoid leaking sensitive content.

### 9. Online Supermarket Integration

The app should support sending shopping lists to online supermarkets such as Morrisons, Tesco, and Sainsbury’s.

### Product position

This is a strategic integration area, but it should be phased after the core planning and shopping workflow is stable.

### Requirements

- Retailer connections belong to a workspace.
- Users can export or send a generated shopping list to a supported retailer.
- The system should track integration status at item and list level where possible.
- Users must be able to review mapped retailer items before final submission when confidence is low.
- If direct cart APIs are unavailable, browser-assisted or export-based approaches may be used as an interim solution.

### Integration model

- Phase 1: exportable shopping list plus a retailer-ready handoff format.
- Phase 2: semi-automated browser/cart population for selected retailers.
- Phase 3: deeper direct integration where retailer capabilities permit.

### Constraints

- Retailer sites and APIs are unstable and may change often.
- Some retailers may not provide official public APIs.
- Credentials, consent, and compliance requirements must be handled carefully.
- The product must degrade gracefully when a retailer integration fails.

## Data Model Direction

This section defines the main domain objects implied by the product.

### Workspace-owned entities

- Recipe
- RecipeIngredient
- RecipeStep
- RecipeNutrition
- RecipeImportJob or RecipeImportSource
- MealPlan
- MealPlanEntry
- ShoppingList
- ShoppingListItem
- RetailerConnection
- RetailerExport or RetailerCartSubmission

### Suggested relationships

- A workspace has many recipes.
- A recipe has many ingredients and many steps.
- A recipe can have one nutrition record.
- A workspace has many meal plans or meal plan entries.
- A shopping list can reference many recipes or meal plan entries as generation sources.
- A retailer export belongs to one shopping list and one workspace.

## User Flows

### Flow A: Save a recipe manually

1. User selects workspace.
2. User creates a recipe.
3. User enters structured ingredients and steps.
4. User saves recipe.
5. Recipe appears in the workspace library.

### Flow B: Import recipe from URL

1. User selects workspace.
2. User submits recipe URL.
3. System imports and parses page.
4. User reviews extracted recipe.
5. User corrects any parsing issues.
6. User saves recipe into workspace library.

### Flow C: Plan the rest of the week

1. User opens weekly planner.
2. User adds recipes to upcoming days.
3. User adjusts servings per planned meal.
4. User reviews the week as a whole.
5. User selects planned meals to shop for.

### Flow D: Generate and use shopping list

1. User generates a list from selected meal plan entries.
2. System consolidates and converts ingredients.
3. User reviews and edits the generated list.
4. User opens shopping mode in-store.
5. User checks off items while shopping.

### Flow E: Cook a planned recipe

1. User opens today’s planned meal.
2. User enters cooking mode.
3. User adjusts servings if needed.
4. User progresses through steps with focused controls.
5. User optionally marks the meal complete.

## Success Metrics

### Adoption metrics

- Number of recipes saved per active workspace.
- Percentage of active workspaces with at least one meal plan entry.
- Percentage of active workspaces with at least one generated shopping list.

### Workflow metrics

- Time from recipe import to saved recipe.
- Time from plan creation to shopping list generation.
- Percentage of shopping list items checked off in shopping mode.
- Percentage of cooking sessions started from a planned meal.

### Quality metrics

- Recipe import success rate.
- Post-import edit rate per imported recipe.
- Ingredient merge correction rate after list generation.
- Retailer export success rate by retailer.

## Non-Functional Requirements

- Mobile-first usability for cooking and shopping modes.
- Reasonable performance for weekly planning and shopping list generation on typical household datasets.
- Clear recoverability when imports, conversions, or integrations fail.
- Auditability of generated shopping lists back to source recipes.
- Secure handling of workspace access and any third-party retailer credentials.

## Delivery Phasing

### Phase 1: Core foundation

- Workspace-owned recipe library
- Manual recipe creation and editing
- Weekly planning
- Shopping list generation from recipes and plans
- Basic dynamic servings and unit conversion

### Phase 2: Import and focused modes

- Recipe import from web URLs with review step
- Cooking mode
- Shopping mode
- Improved ingredient normalization and merge confidence

### Phase 3: Retailer integrations

- Retailer connection model
- Export and handoff flows
- Semi-automated retailer cart support
- Retailer-specific optimization and reliability work

## Open Questions

- Should meal planning be modeled as one weekly object or as independent meal plan entries by date?
- How much ingredient normalization should happen automatically versus through user confirmation?
- Do we want a first-class pantry model later, or should manual shopping-list edits remain the only exception path?
- Which retailer integration approach is acceptable first: export, deep-link handoff, or browser automation?
- Should cooking mode support timers only, or also voice navigation later?

## Recommended Implementation Order

1. Define workspace-owned recipe, meal plan, and shopping-list domain models.
2. Implement recipe CRUD and structured ingredient modeling.
3. Implement weekly meal planning.
4. Implement shopping-list generation and consolidation.
5. Add serving scaling and unit conversion rules.
6. Add recipe import review workflow.
7. Add dedicated cooking mode and shopping mode.
8. Add retailer integration foundations after the core flow is stable.
