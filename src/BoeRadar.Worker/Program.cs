using System.Globalization;
using System.Text.Json;
using BoeRadar.Application;
using BoeRadar.Infrastructure;
using BoeRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var parsed = WorkerOptions.Parse(args);

if (parsed.Error is not null)
{
    Console.Error.WriteLine(parsed.Error);
    Console.Error.WriteLine(WorkerOptions.Usage);
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile(
        new PhysicalFileProvider(AppContext.BaseDirectory),
        "appsettings.json",
        optional: false,
        reloadOnChange: false)
    .AddEnvironmentVariables();
builder.Services.AddBoeRadarInfrastructure(builder.Configuration);

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();

if (parsed.Migrate)
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BoeRadarDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (parsed.Mode is "import" or "pipeline" or "all")
{
    var importer = scope.ServiceProvider.GetRequiredService<ImportOfficialIssue>();
    var result = await importer.ExecuteAsync(parsed.Date!.Value, parsed.Trigger);
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    }));
}

if (parsed.Mode is "analyze" or "pipeline" or "all")
{
    var analyzer = scope.ServiceProvider.GetRequiredService<AnalyzeRadarDocuments>();
    var result = await analyzer.ExecuteAsync(parsed.Date!.Value, parsed.Limit);
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    }));
}

if (parsed.Mode is "digest" or "all")
{
    var digest = scope.ServiceProvider.GetRequiredService<DigestService>();
    var baseUri = new Uri(builder.Configuration["PublicBaseUrl"] ?? "http://localhost:4200");
    var result = await digest.QueueAsync(parsed.Date!.Value, baseUri, parsed.IncludeHeuristic,
        CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(result));
}

if (parsed.Mode is "dispatch" or "all")
{
    var digest = scope.ServiceProvider.GetRequiredService<DigestService>();
    var result = await digest.DispatchAsync(parsed.Limit, CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(result));
}

return 0;

internal sealed record WorkerOptions(
    DateOnly? Date,
    bool Migrate,
    string Trigger,
    string Mode,
    int Limit,
    bool IncludeHeuristic,
    string? Error)
{
    public const string Usage = """
        Uso:
          dotnet run --project src/BoeRadar.Worker -- --date AAAA-MM-DD [--migrate]
            (o --today para usar la fecha de Europe/Madrid)
            [--mode import|analyze|pipeline|digest|dispatch|all] [--limit 100]
            [--include-heuristic]
            [--trigger manual|scheduled|backfill]
        """;

    public static WorkerOptions Parse(IReadOnlyList<string> args)
    {
        DateOnly? date = null;
        var migrate = false;
        var trigger = "manual";
        var mode = "import";
        var limit = 100;
        var includeHeuristic = false;

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--migrate":
                    migrate = true;
                    break;
                case "--today":
                    date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                        DateTimeOffset.UtcNow,
                        TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid")).DateTime);
                    break;
                case "--include-heuristic":
                    includeHeuristic = true;
                    break;
                case "--date" when index + 1 < args.Count:
                    if (!DateOnly.TryParseExact(
                            args[++index],
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var parsedDate))
                    {
                        return new(null, migrate, trigger, mode, limit, includeHeuristic, "--date debe usar el formato AAAA-MM-DD.");
                    }

                    date = parsedDate;
                    break;
                case "--trigger" when index + 1 < args.Count:
                    trigger = args[++index];
                    if (trigger is not ("manual" or "scheduled" or "backfill"))
                    {
                        return new(date, migrate, trigger, mode, limit, includeHeuristic, "--trigger no es válido.");
                    }

                    break;
                case "--mode" when index + 1 < args.Count:
                    mode = args[++index];
                    if (mode is not ("import" or "analyze" or "pipeline" or "digest" or "dispatch" or "all"))
                    {
                        return new(date, migrate, trigger, mode, limit, includeHeuristic, "--mode no es válido.");
                    }

                    break;
                case "--limit" when index + 1 < args.Count:
                    if (!int.TryParse(args[++index], out limit) || limit is < 1 or > 500)
                    {
                        return new(date, migrate, trigger, mode, limit, includeHeuristic, "--limit debe estar entre 1 y 500.");
                    }

                    break;
            }
        }

        return date is null && mode != "dispatch"
            ? new(null, migrate, trigger, mode, limit, includeHeuristic, "Falta el argumento obligatorio --date.")
            : new(date, migrate, trigger, mode, limit, includeHeuristic, null);
    }
}
