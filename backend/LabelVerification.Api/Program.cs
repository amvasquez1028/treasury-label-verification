using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using LabelVerification.Api.Endpoints;
using LabelVerification.Api.Middleware;
using LabelVerification.Api.Services;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Services;
using LabelVerification.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddLabelVerificationCore(builder.Configuration);
builder.Services.AddLabelVerificationInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddSingleton<OcrConcurrencyGate>();
builder.Services.AddSingleton<OcrPerUserLimiter>();

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 20;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("contact", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("verify", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
        opt.QueueLimit = 0;
    });
});

builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:8082");
builder.WebHost.ConfigureKestrel(options =>
{
    // Batch verify can run 2+ minutes before the first response byte (sequential OCR).
    options.Limits.MinResponseDataRate = null;
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
});

var app = builder.Build();

await AuthDbInitializer.InitializeAsync(app.Services);

using (var scope = app.Services.CreateScope())
{
    var engine = scope.ServiceProvider.GetRequiredService<ITesseractEngineProvider>();
    engine.WarmUp();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<StaticExportFallbackMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiEndpoints();

app.MapFallback(async context =>
{
    var webRoot = app.Environment.WebRootPath;
    if (string.IsNullOrWhiteSpace(webRoot))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var rootIndex = Path.Combine(webRoot, "index.html");
    if (File.Exists(rootIndex))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(rootIndex);
        return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
});

app.Run();

public partial class Program { }


