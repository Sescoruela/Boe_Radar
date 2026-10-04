using BoeRadar.Application;
using BoeRadar.Infrastructure.Persistence;
using BoeRadar.Sources;
using Microsoft.EntityFrameworkCore;

namespace BoeRadar.Web;

// Best-effort refresh while the web instance is awake. A sleeping free instance
// cannot provide a guaranteed daily schedule, so the UI exposes its actual date.
public sealed class CatalogRefreshService(
    IServiceScopeFactory scopeFactory,
    ILogger<CatalogRefreshService> logger,
    TimeProvider clock) : BackgroundService
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

    internal async Task RefreshSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BoeRadarDbContext>();
            var today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Madrid).DateTime);
            var importer = scope.ServiceProvider.GetRequiredService<ImportOfficialIssue>();
            var latest = await db.SourceDocuments.AsNoTracking()
                .MaxAsync(item => (DateOnly?)item.PublicationDate, cancellationToken);
            var initialDate = latest is { } last && last < today.AddDays(-6) ? last.AddDays(-6) : today.AddDays(-6);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO catalog_refresh_progress (source, next_date) VALUES ('BOE', {initialDate})
                ON CONFLICT (source) DO NOTHING
                """, cancellationToken);
            var nextDate = await db.CatalogRefreshProgress.AsNoTracking().Where(item => item.Source == "BOE")
                .Select(item => item.NextDate).SingleAsync(cancellationToken);
            // Bound recovery work, persist the contiguous cursor and always recheck recent editions.
            var recovery = Enumerable.Range(0, Math.Min(30, Math.Max(0, today.DayNumber - nextDate.DayNumber + 1)))
                .Select(offset => nextDate.AddDays(offset));
            var dates = recovery.Concat(Enumerable.Range(0, 7).Select(offset => today.AddDays(offset - 6)))
                .Distinct().Order().ToArray();
            foreach (var date in dates)
            {
                var completed = false;
                try
                {
                    var result = await importer.ExecuteAsync(date, "scheduled", cancellationToken);
                    logger.LogInformation("BOE {Date}: {Created} publicaciones nuevas.", date, result.Created);
                    completed = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (BoeSourceUnavailableException exception)
                    when (exception.Message.StartsWith("El BOE no dispone de información", StringComparison.Ordinal))
                {
                    logger.LogInformation("No hay sumario del BOE para el {Date}.", date);
                    completed = date < today; // Today's BOE might not have been published yet.
                }
                catch (Exception exception)
                {
                    // Non-publication days and temporary source failures do not stop the web.
                    logger.LogWarning(exception, "No se pudo actualizar el BOE del {Date}; se reintentará.", date);
                }
                if (completed && date == nextDate)
                {
                    var following = date.AddDays(1);
                    await db.CatalogRefreshProgress.Where(item => item.Source == "BOE" && item.NextDate == date)
                        .ExecuteUpdateAsync(update => update.SetProperty(item => item.NextDate, following), cancellationToken);
                    nextDate = following;
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
