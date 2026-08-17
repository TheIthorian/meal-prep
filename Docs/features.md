# Features and How To Use Them

This guide explains the main user-facing features in Meal Prep.

## 1) Workspaces

Use workspaces to separate personal/team contexts and data.

- Select or create a workspace after login.
- Most data operations are scoped to the active workspace.

## 2) Recipe Management

Use recipes as the core content unit.

- Create recipes manually.
- Update ingredients and metadata as needed.
- Organize your saved recipe catalog over time.

## 3) Recipe Import

Import recipes from external pages to reduce manual entry.

- Paste or submit a recipe URL in the import flow.
- Review parsed content after import.
- Adjust parsed fields if needed.

## 4) Importing a Shared Collection

Open a collection share link to copy someone else's collection into one of your workspaces.

- Pick the workspace to import into and start the import.
- Progress is reported per recipe (for example "4 of 12"), not as an indefinite spinner.
- The import runs on the server, so you can close the page and come back to the same link to see how
  far it got.
- If some recipes could not be imported they are listed by name, and a retry re-runs only those.

## 5) Shopping Lists

Generate shopping lists from selected recipes.

- Pick one or more recipes.
- Build a shopping list from ingredient data.
- Use the list while shopping and update progress.

## 6) Offline Shopping Support

The UI supports offline-oriented shopping workflows.

- Open your shopping list before leaving connectivity.
- Continue checking items while offline.
- Sync updates when connection is restored.

## 7) MCP Integrations

Connect supported MCP clients directly to your workspace.

- Open Integrations in app settings.
- Generate an MCP URL for a chosen workspace.
- Add the URL to your MCP client.
- Revoke any token when no longer needed.

Security tip: treat MCP URLs as secrets.
