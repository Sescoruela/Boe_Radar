using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BoeRadar.Infrastructure.Persistence;

public sealed class BoeRadarDbContextFactory : IDesignTimeDbContextFactory<BoeRadarDbContext>
{
    public BoeRadarDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BOERADAR_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=54329;Database=boeradar;Username=boeradar;Password=boeradar_dev";
        var options = new DbContextOptionsBuilder<BoeRadarDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BoeRadarDbContext(options);
    }
}

