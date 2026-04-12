using DotNetEnv;
using Api.Startup;
using Hangfire;
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

var app = builder.Build();

app.LogStartupConfiguration();
await app.ApplyMigrationsAsync();
app.RegisterRecurringJobs();

var forwardedHeadersOptions = new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                       | ForwardedHeaders.XForwardedProto
                       | ForwardedHeaders.XForwardedHost
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseGlobalExceptionHandler();
app.MapApiEndpoints();
app.UseApiPipeline();

if (builder.Environment.IsDevelopment())
    app.UseHangfireDashboard();

app.Run();

public partial class Program { }
