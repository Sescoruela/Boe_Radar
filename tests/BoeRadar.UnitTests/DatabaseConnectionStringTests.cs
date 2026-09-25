using BoeRadar.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace BoeRadar.UnitTests;

public sealed class DatabaseConnectionStringTests
{
    [Fact]
    public void ConvertsRenderPostgresUrlIncludingEncodedCredentials()
    {
        var result = DatabaseConnectionString.FromPostgresUrl(
            "postgresql://radar:p%40ss%3Aword@db.internal:5433/boe_radar");

        var options = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("db.internal", options.Host);
        Assert.Equal(5433, options.Port);
        Assert.Equal("boe_radar", options.Database);
        Assert.Equal("radar", options.Username);
        Assert.Equal("p@ss:word", options.Password);
    }

    [Fact]
    public void PrefersRenderUrlOverDevelopmentConnectionString()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = "postgresql://radar:secret@render-db/boe",
                ["ConnectionStrings:BoeRadar"] = "Host=localhost;Database=dev"
            }).Build();

        var options = new NpgsqlConnectionStringBuilder(
            DatabaseConnectionString.Resolve(configuration));
        Assert.Equal("render-db", options.Host);
        Assert.Equal("boe", options.Database);
    }

    [Theory]
    [InlineData("http://db/boe")]
    [InlineData("postgresql://db/boe")]
    [InlineData("postgresql://user:pass@db/")]
    public void RejectsInvalidUrls(string databaseUrl)
    {
        Assert.Throws<ArgumentException>(() =>
            DatabaseConnectionString.FromPostgresUrl(databaseUrl));
    }
}
