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

## 4) Shopping Lists

Generate shopping lists from selected recipes.

- Pick one or more recipes.
- Build a shopping list from ingredient data.
- Use the list while shopping and update progress.

## 5) Offline Shopping Support

The UI supports offline-oriented shopping workflows.

- Open your shopping list before leaving connectivity.
- Continue checking items while offline.
- Sync updates when connection is restored.

## 6) Collection Share Links

Share a recipe collection with anyone using a magic link.

- Open a collection and create a share link.
- Send the link (`/share/recipe-collections/<token>`) to anyone.
- Recipients without an account see a read-only view of the collection plus a prompt to create an
  account or sign in. The link is preserved through sign-up/sign-in, so they land back on the shared
  collection and can import it in one step.
- Recipients with an account choose a workspace and import the collection into it.

Security tip: treat share links as secrets — anyone holding the token can read the collection.

## 7) MCP Integrations

Connect supported MCP clients directly to your workspace.

- Open Integrations in app settings.
- Generate an MCP URL for a chosen workspace.
- Add the URL to your MCP client.
- Revoke any token when no longer needed.

Security tip: treat MCP URLs as secrets.
