using System.Net;
using System.Net.Http;
using System.Text;
using Api.Models;
using Api.Services.MealPrep;

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
        var service = new RecipeImportService(httpClient, new MeasurementService());

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
    public async Task PreviewAsync_ShouldRejectLocalhostImports()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler("<html></html>"));
        var service = new RecipeImportService(httpClient, new MeasurementService());

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
        var service = new RecipeImportService(httpClient, new MeasurementService());

        var exception = await Assert.ThrowsAsync<Api.Domain.InvalidFormatException>(() =>
            service.PreviewAsync("https://example.com/recipe")
        );

        Assert.Contains("too large", exception.Details.Detail);
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
