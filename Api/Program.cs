using DotNetEnv;
using Api.Startup;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

Env.Load();
builder.Configuration.AddEnvironmentVariables();

builder.AddAppLogging();
builder.AddAppOpenTelemetry();
builder.Services.AddAppSwagger();

builder.AddAppDatabase();
builder.AddAuthStateStorage();
builder.Services.AddIdentityAndAuth(builder.Environment);
builder.Services.AddFrontendCors(builder.Configuration);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddAppCompression();
builder.Services.AddMealPrepMcpServer();

var app = builder.Build();

app.LogStartupConfiguration();
app.LogStartupUrls();
await app.ApplyMigrationsAsync();

// Railway (and the local Caddy container) put exactly one proxy in front of the API, and its
// address is not knowable ahead of time on a platform that reschedules containers. So rather than
// naming trusted proxies, trust a single hop: ForwardLimit = 1 means a client that sends its own
// X-Forwarded-For has that value discarded in favour of the one the real proxy appends.
// XForwardedHost is deliberately absent — nothing derives absolute URLs from the host, and
// accepting it would let a caller choose the host the app believes it is serving.
var forwardedHeadersOptions = new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);
// Compression sits ahead of everything that writes a body so no later middleware can emit an
// uncompressed response, and ahead of the endpoints that read gzipped import bodies.
app.UseAppCompression();
// CORS must run before HTTPS redirection so preflight OPTIONS is not redirected (browsers forbid that)
// and so redirect responses include Access-Control-Allow-Origin.
app.UseCors("Frontend");
if (!app.Environment.IsDevelopment()) {
    app.UseHttpsRedirection();
}

app.UseGlobalExceptionHandler();
app.MapApiEndpoints();
app.MapMealPrepMcpEndpoints();
app.UseApiPipeline();

app.Run();

public partial class Program { }
