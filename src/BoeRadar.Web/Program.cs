using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BoeRadar.Application;
using BoeRadar.Domain;
using BoeRadar.Infrastructure;
using BoeRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter<RadarCategory>(allowIntegerValues: false)));
builder.Services.AddBoeRadarInfrastructure(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("subscription-register", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("development", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<BoeRadarDbContext>()
        .Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/subscriptions"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseCors("development");
    app.MapOpenApi();
}

var api = app.MapGroup("/api/v1");

api.MapGet("/publications", async (
        IPublicationCatalog catalog,
        string? query,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? section,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
    {
        if (dateFrom > dateTo)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["dateRange"] = ["dateFrom no puede ser posterior a dateTo."]
            });
        }

        var result = await catalog.SearchAsync(
            new PublicationSearch(query, dateFrom, dateTo, section, page, pageSize),
            cancellationToken);
        return Results.Ok(result);
    })
    .WithName("SearchPublications")
    .WithSummary("Busca publicaciones importadas del BOE.");

api.MapGet("/publications/{id:guid}", async (
        Guid id,
        IPublicationCatalog catalog,
        CancellationToken cancellationToken) =>
    {
        var publication = await catalog.GetAsync(id, cancellationToken);
        return publication is null ? Results.NotFound() : Results.Ok(publication);
    })
    .WithName("GetPublication")
    .WithSummary("Obtiene una publicación y sus enlaces oficiales.");

var subscriptions = api.MapGroup("/subscriptions");
var publicBaseUrl = new Uri(builder.Configuration["RENDER_EXTERNAL_URL"]
    ?? builder.Configuration["PublicBaseUrl"]
    ?? "http://localhost:4200");

subscriptions.MapPost("", async (RegisterSubscriptionRequest request,
    SubscriptionService service, CancellationToken cancellationToken) =>
{
    if (!request.Consent)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["consent"] = ["Debes aceptar recibir alertas por correo."]
        });
    try
    {
        await service.RegisterAsync(request.Email,
            new SubscriptionPreferences(request.Categories ?? [], request.Keywords ?? [], request.DigestHour),
            publicBaseUrl, cancellationToken);
        return Results.Accepted(value: new { message = "Si procede, recibirás un correo de verificación." });
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["subscription"] = [exception.Message]
        });
    }
}).RequireRateLimiting("subscription-register");

subscriptions.MapPost("/verify", async (TokenRequest request,
    SubscriptionService service, CancellationToken cancellationToken) =>
{
    var token = await service.VerifyAsync(request.Token, publicBaseUrl, cancellationToken);
    return token is null ? Results.BadRequest(new { message = "El enlace no es válido o ha caducado." })
        : Results.Ok(new { managementToken = token });
});

subscriptions.MapGet("/me", async (HttpRequest request, SubscriptionService service,
    CancellationToken cancellationToken) =>
{
    var current = await service.GetAsync(request.Headers["X-Management-Token"].ToString(), cancellationToken);
    return current is null ? Results.Unauthorized() : Results.Ok(current);
});

subscriptions.MapPut("/me", async (HttpRequest request, SubscriptionPreferences preferences,
    SubscriptionService service, CancellationToken cancellationToken) =>
{
    try
    {
        var changed = await service.UpdateAsync(request.Headers["X-Management-Token"].ToString(),
            preferences, cancellationToken);
        return changed ? Results.NoContent() : Results.Unauthorized();
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["preferences"] = [exception.Message]
        });
    }
});

subscriptions.MapPost("/unsubscribe", async (TokenRequest request,
    SubscriptionService service, CancellationToken cancellationToken) =>
{
    var changed = await service.UnsubscribeAsync(request.Token, cancellationToken);
    return changed ? Results.NoContent() : Results.BadRequest(new { message = "El enlace no es válido." });
});

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (
    BoeRadarDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected" })
        : Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Database unavailable");
});

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

public sealed record RegisterSubscriptionRequest(string Email,
    IReadOnlyList<RadarCategory>? Categories, IReadOnlyList<string>? Keywords,
    int DigestHour, bool Consent);
public sealed record TokenRequest(string Token);
