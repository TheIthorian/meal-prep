using System.ComponentModel;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Api.Mcp;

/// <summary>
///     Builds <see cref="McpServerTool" /> instances for <see cref="MealPrepMcpTools" /> with strict JSON Schema for tool arguments
///     (advertised to clients after the MCP initialize handshake via tools/list).
/// </summary>
internal static class MealPrepMcpToolsRegistration
{
    internal static IEnumerable<McpServerTool> CreateTools()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
        var serializerOptions = McpJson.SerializerOptions;
        var transformOptions = new AIJsonSchemaTransformOptions
        {
            DisallowAdditionalProperties = true,
            RequireAllProperties = false,
        };

        var schemaCreateOptions = new AIJsonSchemaCreateOptions { TransformOptions = transformOptions };

        foreach (var method in typeof(MealPrepMcpTools).GetMethods(flags))
        {
            if (method.GetCustomAttribute<McpServerToolAttribute>() is null)
                continue;

            var opts = new McpServerToolCreateOptions
            {
                Name = BuildToolName(method.Name),
                SerializerOptions = serializerOptions,
                SchemaCreateOptions = schemaCreateOptions,
            };
            ApplyToolBehaviorHints(opts, method.Name);

            if (method.GetCustomAttribute<DescriptionAttribute>() is { Description: { } description })
                opts.Description = description;

            yield return McpServerTool.Create(
                method,
                static ctx =>
                    ctx.Services?.GetRequiredService<MealPrepMcpTools>()
                    ?? throw new InvalidOperationException("MCP request has no service provider."),
                opts
            );
        }
    }

    private static string BuildToolName(string methodName)
    {
        const string mealPrepPrefix = "MealPrep";
        var withoutPrefix = methodName.StartsWith(mealPrepPrefix, StringComparison.Ordinal)
            ? methodName[mealPrepPrefix.Length..]
            : methodName;

        return ToSnakeCase(withoutPrefix);
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current))
            {
                if (i > 0)
                {
                    var previous = value[i - 1];
                    var hasNext = i + 1 < value.Length;
                    var next = hasNext ? value[i + 1] : '\0';
                    var shouldInsertUnderscore = char.IsLower(previous) || (hasNext && char.IsLower(next));
                    if (shouldInsertUnderscore)
                        builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static void ApplyToolBehaviorHints(McpServerToolCreateOptions options, string methodName)
    {
        var (readOnly, destructive, idempotent, openWorld) = methodName switch
        {
            nameof(MealPrepMcpTools.GetCurrentUser) => (true, false, true, false),
            nameof(MealPrepMcpTools.ListWorkspaces) => (true, false, true, false),
            
            nameof(MealPrepMcpTools.ListRecipes) => (true, false, true, false),
            nameof(MealPrepMcpTools.GetRecipe) => (true, false, true, false),
            
            nameof(MealPrepMcpTools.CreateRecipe) => (false, false, false, false),
            nameof(MealPrepMcpTools.UpdateRecipe) => (false, true, false, false),
            nameof(MealPrepMcpTools.SetRecipeImageFromUrl) => (false, true, false, true),
            nameof(MealPrepMcpTools.DeleteRecipe) => (false, true, false, false),
            nameof(MealPrepMcpTools.ImportRecipe) => (false, false, false, true),
            nameof(MealPrepMcpTools.ListMealPlanEntries) => (true, false, true, false),
            nameof(MealPrepMcpTools.PutMealPlanEntry) => (false, true, false, false),
            nameof(MealPrepMcpTools.DeleteMealPlanEntry) => (false, true, false, false),
            nameof(MealPrepMcpTools.ListShoppingLists) => (true, false, true, false),
            nameof(MealPrepMcpTools.GetShoppingList) => (true, false, true, false),
            nameof(MealPrepMcpTools.GenerateShoppingList) => (false, false, false, false),
            nameof(MealPrepMcpTools.UpdateShoppingList) => (false, true, false, false),
            nameof(MealPrepMcpTools.DeleteShoppingList) => (false, true, false, false),
            nameof(MealPrepMcpTools.CreateShoppingListItem) => (false, false, false, false),
            nameof(MealPrepMcpTools.UpdateShoppingListItem) => (false, true, false, false),
            nameof(MealPrepMcpTools.DeleteShoppingListItem) => (false, true, false, false),
            _ => (false, true, false, true)
        };

        options.ReadOnly = readOnly;
        options.Destructive = destructive;
        options.Idempotent = idempotent;
        options.OpenWorld = openWorld;
    }
}
