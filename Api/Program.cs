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
builder.Services.AddMealPrepMcpServer();

var app = builder.Build();

app.LogStartupConfiguration();
await app.ApplyMigrationsAsync();

var forwardedHeadersOptions = new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                       | ForwardedHeaders.XForwardedProto
                       | ForwardedHeaders.XForwardedHost
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);
// CORS must run before HTTPS redirection so preflight OPTIONS is not redirected (browsers forbid that)
// and so redirect responses include Access-Control-Allow-Origin.
app.UseCors("Frontend");
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseGlobalExceptionHandler();
app.MapApiEndpoints();
app.MapMealPrepMcpEndpoints();
app.UseApiPipeline();

app.Run();

public partial class Program { }
