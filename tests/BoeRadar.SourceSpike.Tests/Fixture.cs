namespace BoeRadar.SourceSpike.Tests;

internal static class Fixture
{
    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
}

