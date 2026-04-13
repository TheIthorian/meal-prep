using System.Net;
using System.Net.Http;
using System.Text;
using Api.Configuration;
using Api.Data;
using Api.Models;
using Api.Services.MealPrep;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Services.MealPrep;

public class RecipeImportServiceTests
{
    [Fact]
    public async Task PreviewAsync_ShouldExtractStructuredRecipeFromJsonLd()
    {
        var html = """
            <html>
              <head>
                <title>Example Recipe</title>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "Recipe",
                  "name": "Lemon Pasta",
                  "description": "Bright, quick pasta.",
                  "recipeYield": "4 servings",
                  "prepTime": "PT10M",
                  "cookTime": "PT15M",
                  "recipeIngredient": [
                    "200 g spaghetti",
                    "1 lemon"
                  ],
                  "recipeInstructions": [
                    { "text": "Boil the pasta for 10 minutes." },
                    { "text": "Mix with lemon zest and serve." }
                  ],
                  "nutrition": {
                    "@type": "NutritionInformation",
                    "calories": "420 calories",
                    "proteinContent": "14 g"
                  },
                  "keywords": "quick, dinner"
                }
                </script>
              </head>
              <body></body>
            </html>
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(html));
        var service = CreateRecipeImportService(httpClient);

        var preview = await service.PreviewAsync("https://example.com/lemon-pasta");

        Assert.Equal("Lemon Pasta", preview.Title);
        Assert.Equal(4m, preview.Servings);
        Assert.Equal(2, preview.Ingredients.Count);
        Assert.Equal(2, preview.Steps.Count);
        Assert.Contains("quick", preview.Tags);
        Assert.Equal(4m, preview.Nutrition?.ServingBasis);
        Assert.Equal(420m, preview.Nutrition?.Nutrients.Single(nutrient => nutrient.NutrientType == RecipeNutrientTypes.Calories).Amount);
        Assert.Equal(14m, preview.Nutrition?.Nutrients.Single(nutrient => nutrient.NutrientType == RecipeNutrientTypes.Protein).Amount);
    }

    [Fact]
    public async Task PreviewAsync_ShouldParseNumericRecipeYieldInJsonLd()
    {
        var html = """
            <html>
              <head>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "Recipe",
                  "name": "Test Dish",
                  "recipeYield": 6,
                  "recipeIngredient": [ "1 cup rice" ],
                  "recipeInstructions": [ { "text": "Cook." } ]
                }
                </script>
              </head>
              <body></body>
            </html>
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(html));
        var service = CreateRecipeImportService(httpClient);

        var preview = await service.PreviewAsync("https://example.com/numeric-yield");

        Assert.Equal("Test Dish", preview.Title);
        Assert.Equal(6m, preview.Servings);
    }

    [Fact]
    public async Task PreviewAsync_ShouldParseMixedUnicodeFractionAmountsInJsonLd()
    {
        var html = """
            <html>
              <head>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "Recipe",
                  "name": "Fried Test",
                  "recipeIngredient": [ "1 ¼ cups vegetable oil" ],
                  "recipeInstructions": [ { "text": "Heat oil." } ]
                }
                </script>
              </head>
              <body></body>
            </html>
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(html));
        var service = CreateRecipeImportService(httpClient);

        var preview = await service.PreviewAsync("https://example.com/fried-test");

        var oil = Assert.Single(preview.Ingredients);
        Assert.Equal(1.25m, oil.Amount);
        Assert.Equal("cups", oil.Unit);
        Assert.Equal("vegetable oil", oil.Name);
    }

    [Fact]
    public async Task PreviewAsync_ShouldRejectLocalhostImports()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler("<html></html>"));
        var service = CreateRecipeImportService(httpClient);

        var exception = await Assert.ThrowsAsync<Api.Domain.InvalidFormatException>(() =>
            service.PreviewAsync("http://localhost/recipe")
        );

        Assert.Contains("Local or private network", exception.Details.Detail);
    }

    [Fact]
    public async Task PreviewAsync_ShouldRejectOversizedResponses()
    {
        var oversizedHtml = new string('a', 2 * 1024 * 1024 + 1);
        var httpClient = new HttpClient(new StubHttpMessageHandler(oversizedHtml, oversizedHtml.Length));
        var service = CreateRecipeImportService(httpClient);

        var exception = await Assert.ThrowsAsync<Api.Domain.InvalidFormatException>(() =>
            service.PreviewAsync("https://example.com/recipe")
        );

        Assert.Contains("too large", exception.Details.Detail);
    }

    [Fact]
    public async Task PreviewAsync_WhenHeuristicsFailAndLlmDisabled_Throws()
    {
        var html = """
            <html>
              <head><title>Not a recipe</title></head>
              <body><p>Hello world</p></body>
            </html>
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(html));
        var service = CreateRecipeImportService(httpClient);

        var exception = await Assert.ThrowsAsync<Api.Domain.InvalidFormatException>(() =>
            service.PreviewAsync("https://example.com/page")
        );

        Assert.Contains("Could not find recipe metadata", exception.Details.Detail);
    }

    private static RecipeImportService CreateRecipeImportService(HttpClient httpClient)
    {
        var llmParser = new RecipeImportLlmParser(
            Options.Create(new OpenAIConfiguration { ApiKey = string.Empty }),
            NullLogger<RecipeImportLlmParser>.Instance
        );

        var dbOptions = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RecipeImportService(
            httpClient,
            new MeasurementService(),
            llmParser,
            new ApiDbContext(dbOptions),
            NullLogger<RecipeImportService>.Instance
        );
    }

    private sealed class StubHttpMessageHandler(string html, long? contentLength = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(html, Encoding.UTF8, "text/html");

            if (contentLength.HasValue) response.Content.Headers.ContentLength = contentLength.Value;

            return Task.FromResult(response);
        }
    }
}
