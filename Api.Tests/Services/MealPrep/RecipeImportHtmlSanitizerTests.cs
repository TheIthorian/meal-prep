using Api.Services.MealPrep;

namespace Api.Tests.Services.MealPrep;

public class RecipeImportHtmlSanitizerTests
{
    [Fact]
    public void Sanitize_ShouldRemoveScriptBlocks() {
        var html = """
                   <html><body>
                     <script>var tracking = {a:1}; console.log("noise");</script>
                     <p>200 g spaghetti</p>
                   </body></html>
                   """;

        var sanitized = RecipeImportHtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("tracking", sanitized);
        Assert.DoesNotContain("<script", sanitized);
        Assert.Contains("200 g spaghetti", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldKeepJsonLdScriptContent() {
        var html = """
                   <html><head>
                     <script type="application/ld+json">{"@type":"Recipe","name":"Lemon Pasta"}</script>
                     <script type="text/javascript">var noise = 1;</script>
                   </head><body></body></html>
                   """;

        var sanitized = RecipeImportHtmlSanitizer.Sanitize(html);

        Assert.Contains("\"@type\":\"Recipe\"", sanitized);
        Assert.Contains("Lemon Pasta", sanitized);
        Assert.DoesNotContain("var noise", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldRemoveStyleBlocks() {
        var html = """
                   <html><head><style>.hero{background:url(x.png);color:#fff}</style></head>
                   <body><p>Bright, quick pasta.</p></body></html>
                   """;

        var sanitized = RecipeImportHtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("background", sanitized);
        Assert.DoesNotContain("<style", sanitized);
        Assert.Contains("Bright, quick pasta.", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldRemoveSvgBlocks() {
        var html = """
                   <html><body>
                     <svg viewBox="0 0 24 24"><path d="M12 2L2 22h20L12 2z"/></svg>
                     <p>Boil the pasta.</p>
                   </body></html>
                   """;

        var sanitized = RecipeImportHtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("M12 2L2 22h20L12 2z", sanitized);
        Assert.DoesNotContain("<svg", sanitized);
        Assert.Contains("Boil the pasta.", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldRemoveHtmlComments() {
        var html = "<html><body><!-- ad slot: 12345 --><p>1 lemon</p></body></html>";

        var sanitized = RecipeImportHtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("ad slot", sanitized);
        Assert.DoesNotContain("<!--", sanitized);
        Assert.Contains("1 lemon", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldKeepMetaTags() {
        var html = """
                   <html><head>
                     <meta property="og:title" content="Lemon Pasta"/>
                     <meta name="description" content="Bright, quick pasta."/>
                   </head><body><p>Serves 4</p></body></html>
                   """;

        var sanitized = RecipeImportHtmlSanitizer.Sanitize(html);

        Assert.Contains("""<meta property="og:title" content="Lemon Pasta"/>""", sanitized);
        Assert.Contains("""<meta name="description" content="Bright, quick pasta."/>""", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldCollapseWhitespaceLeftBehindByStrippedMarkup() {
        var html = """
                   <html><body>
                     <div>


                       <span>200   g     spaghetti</span>

                     </div>
                     <script>var a = 1;</script>


                     <div>1 lemon</div>
                   </body></html>
                   """;

        var sanitized = RecipeImportHtmlSanitizer.Sanitize(html);

        Assert.Equal("200 g spaghetti\n1 lemon", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldReturnEmptyForBlankInput() {
        Assert.Equal(string.Empty, RecipeImportHtmlSanitizer.Sanitize("   \n  "));
    }

    [Fact]
    public void Sanitize_ShouldShrinkAScriptHeavyPage() {
        var noise = string.Concat(Enumerable.Repeat("<script>var x = 1;</script><style>.a{color:red}</style>", 500));
        var html = $"<html><head>{noise}</head><body><p>200 g spaghetti</p></body></html>";

        var sanitized = RecipeImportHtmlSanitizer.Sanitize(html);

        Assert.Equal("200 g spaghetti", sanitized);
        Assert.True(sanitized.Length < html.Length / 100);
    }
}
