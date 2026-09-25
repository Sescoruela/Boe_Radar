using BoeRadar.Application;
using BoeRadar.Infrastructure.Persistence;
using BoeRadar.Sources;
using Microsoft.EntityFrameworkCore;

namespace BoeRadar.Web;

// Best-effort refresh while the web instance is awake. A sleeping free instance
// cannot provide a guaranteed daily schedule, so the UI exposes its actual date.
public sealed class CatalogRefreshService(
    IServiceScopeFactory scopeFactory,
    ILogger<CatalogRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshSafelyAsync(stoppingToken);
        }
    }

    private async Task RefreshSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BoeRadarDbContext>();
            var today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Madrid).DateTime);
            var importer = scope.ServiceProvider.GetRequiredService<ImportOfficialIssue>();
            for (var date = today.AddDays(-6); date <= today; date = date.AddDays(1))
            {
                if (await db.SourceDocuments.AsNoTracking()
                        .AnyAsync(item => item.PublicationDate == date, cancellationToken))
                {
                    continue;
                }

                try
                {
                    var result = await importer.ExecuteAsync(date, "scheduled", cancellationToken);
                    logger.LogInformation("BOE {Date}: {Created} publicaciones nuevas.", date, result.Created);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (BoeSourceUnavailableException exception)
                    when (exception.Message.StartsWith("El BOE no dispone de información", StringComparison.Ordinal))
                {
                    logger.LogInformation("No hay sumario del BOE para el {Date}.", date);
                }
                catch (Exception exception)
                {
                    // Non-publication days and temporary source failures do not stop the web.
                    logger.LogWarning(exception, "No se pudo actualizar el BOE del {Date}; se reintentará.", date);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "La actualización del catálogo falló; se reintentará.");
        }
    }
}
