namespace Api.Configuration;

/// <summary>
///     Location of the web app, used to build links the API hands to clients (MCP responses, for now).
///     Required: a wrong or missing base URL produces links that look fine and go nowhere, so startup fails instead.
/// </summary>
public class WebAppOptions
{
    public const string SectionName = "WebApp";

    public const string RequiredMessage =
        "WebApp:BaseUrl is required. Set 'WebApp:BaseUrl' or environment variable 'WEB_APP_BASE_URL' "
        + "to the web app's origin, e.g. https://mealprep.example.";

    /// <summary>
    ///     Origin of the web app, without a trailing slash.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Accepts the flat environment variable as an alias for the section key, matching how AppRoles is configured.
    /// </summary>
    public void Normalize(string? flatBaseUrl) {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            BaseUrl = flatBaseUrl ?? string.Empty;
        BaseUrl = BaseUrl.Trim().TrimEnd('/');
    }

    public bool IsValid() {
        return Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
