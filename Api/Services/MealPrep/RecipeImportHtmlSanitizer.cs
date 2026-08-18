using System.Text.RegularExpressions;

namespace Api.Services.MealPrep;

/// <summary>
///     Reduces a fetched recipe page to the smallest payload that still lets the LLM fallback parser find the recipe.
///     Only the LLM fallback path uses this: the JSON-LD and HTML-heuristic extractors run first and parse the raw HTML.
/// </summary>
/// <remarks>
///     <para>
///         Why text-plus-metadata rather than "strip the noise blocks and keep the remaining tags": both were measured
///         against https://www.bbcgoodfood.com/recipes/easy-chocolate-cake (576,886 raw chars). Removing scripts, styles,
///         SVGs and comments but keeping the surviving tags leaves 182,119 chars — still well above the 120,000-char
///         truncation cap, so that variant would have sent exactly as many tokens as before and saved nothing. Collapsing
///         the remaining markup to text while preserving <c>&lt;meta&gt;</c> tags and JSON-LD leaves 15,195 chars, an
///         ~8x cut in what actually reaches the model.
///     </para>
///     <para>
///         What is kept, and why: <c>&lt;meta&gt;</c> tags survive verbatim because og:/twitter:/itemprop metadata carries
///         the title, description and hero image; <c>&lt;script type="application/ld+json"&gt;</c> bodies survive because
///         the structured-data extractor that ran earlier may have rejected a shape it did not recognise while the model
///         can still read it. The cost of this choice is the per-element structure (<c>itemprop</c>, class names) that
///         might help tell an ingredient list from body copy — accepted, because keeping it did not fit under the cap.
///         Tags are replaced with line breaks rather than spaces so list items and headings stay on separate lines.
///     </para>
///     <para>
///         This also improves accuracy, not just cost. Previously the 120,000-char truncation was applied to
///         script-heavy raw HTML, so on a large page the recipe itself could fall past the cut and never reach the
///         model at all. After sanitizing, a typical page lands far below the cap and truncation rarely bites.
///     </para>
/// </remarks>
public static partial class RecipeImportHtmlSanitizer
{
    /// <summary>
    ///     Strips scripts (except JSON-LD), styles, SVGs and comments, collapses the remaining markup to text while
    ///     keeping <c>&lt;meta&gt;</c> tags, and normalizes whitespace.
    /// </summary>
    public static string Sanitize(string html) {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var stripped = HtmlComment().Replace(html, "\n");
        stripped = ScriptBlock().Replace(stripped, static match => IsJsonLdScript(match.Groups[1].Value) ? match.Value : "\n");
        stripped = StyleBlock().Replace(stripped, "\n");
        stripped = SvgBlock().Replace(stripped, "\n");
        stripped = AnyTag().Replace(stripped, static match => MetaTag().IsMatch(match.Value) ? match.Value : "\n");

        return CollapseWhitespace(stripped);
    }

    private static bool IsJsonLdScript(string attributes) {
        return attributes.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase);
    }

    private static string CollapseWhitespace(string value) {
        var collapsed = LineBreaks().Replace(value, "\n");
        collapsed = HorizontalWhitespace().Replace(collapsed, " ");
        collapsed = PaddedLineBreak().Replace(collapsed, "\n");

        return collapsed.Trim();
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"<script\b([^>]*)>.*?</script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptBlock();

    [GeneratedRegex(@"<style\b[^>]*>.*?</style\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleBlock();

    [GeneratedRegex(@"<svg\b[^>]*>.*?</svg\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SvgBlock();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"^<meta\b", RegexOptions.IgnoreCase)]
    private static partial Regex MetaTag();

    [GeneratedRegex(@"\r\n?")]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex HorizontalWhitespace();

    [GeneratedRegex(@" ?\n[\s\n]*")]
    private static partial Regex PaddedLineBreak();
}
