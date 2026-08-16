using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

namespace Api.Startup;

/// <summary>
///     Registers HTTP compression for responses the API sends and for request bodies clients send.
/// </summary>
public static class CompressionServiceCollectionExtensions
{
    /// <summary>
    ///     Auth responses are excluded from compression: they mix a secret (the identity token or
    ///     cookie) with caller-controlled input in the same response, which is the shape BREACH
    ///     attacks a compressed channel with. Everything else is workspace data the caller already
    ///     owns, so compressing it costs nothing security-wise.
    /// </summary>
    private const string UncompressedPathPrefix = "/api/v1/auth";

    /// <summary>
    ///     JSON payloads the UI pulls in bulk (paged recipe lists, collection exports) are not in the
    ///     default MIME list, so they are named explicitly here alongside the problem-details type.
    /// </summary>
    private static readonly string[] AdditionalMimeTypes = [
        "application/json",
        "application/problem+json",
    ];

    extension(IServiceCollection services)
    {
        public void AddAppCompression() {
            services.AddResponseCompression(options => {
                    // The API is served over HTTPS everywhere except local development, so leaving
                    // this off would mean never compressing anything in production.
                    options.EnableForHttps = true;
                    options.Providers.Add<BrotliCompressionProvider>();
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(AdditionalMimeTypes);
                }
            );

            // Fastest keeps the per-request CPU cost low; on JSON this still gives most of the
            // size win, and the API is latency-sensitive rather than bandwidth-constrained.
            services.Configure<BrotliCompressionProviderOptions>(options => {
                    options.Level = CompressionLevel.Fastest;
                }
            );
            services.Configure<GzipCompressionProviderOptions>(options => {
                    options.Level = CompressionLevel.Fastest;
                }
            );

            // Collection bundle imports POST one full recipe body per recipe, up to the 1000-recipe
            // cap, so clients are allowed to send those gzipped.
            services.AddRequestDecompression();
        }
    }

    extension(WebApplication app)
    {
        public void UseAppCompression() {
            app.UseRequestDecompression();
            app.UseWhen(
                context => !context.Request.Path.StartsWithSegments(UncompressedPathPrefix),
                branch => branch.UseResponseCompression()
            );
        }
    }
}
