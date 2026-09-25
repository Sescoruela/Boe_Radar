using System.Net.Http.Headers;
using BoeRadar.Application;
using BoeRadar.Infrastructure.Persistence;
using BoeRadar.Infrastructure.Sources;
using BoeRadar.Infrastructure.Analysis;
using BoeRadar.Infrastructure.Messaging;
using BoeRadar.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Google.GenAI;

namespace BoeRadar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBoeRadarInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DatabaseConnectionString.Resolve(configuration);

        services.AddDbContext<BoeRadarDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(BoeRadarDbContext).Assembly.FullName)));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BoeSummaryParser>();
        services.AddSingleton<OfficialDocumentContentExtractor>();
        services.AddHttpClient<BoeOpenDataClient>(client =>
        {
            client.BaseAddress = BoeOpenDataClient.DefaultBaseAddress;
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("BOE-Radar-IA", "0.2"));
        });
        services.AddScoped<IOfficialGazetteSource, BoeOfficialGazetteSource>();
        services.AddScoped<IIngestionStore, EfIngestionStore>();
        services.AddScoped<IPublicationCatalog, EfPublicationCatalog>();
        services.AddScoped<IAnalysisCandidateStore, EfAnalysisCandidateStore>();
        services.AddScoped<ISubscriptionStore, EfSubscriptionStore>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<DigestService>();
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        var analysisProvider = configuration["Analysis:Provider"] ?? "heuristic";
        if (analysisProvider.Equals("gemini", StringComparison.OrdinalIgnoreCase))
        {
            var project = configuration["Analysis:Gemini:Project"]
                ?? throw new InvalidOperationException("Falta Analysis:Gemini:Project.");
            var location = configuration["Analysis:Gemini:Location"] ?? "europe-west1";
            var model = configuration["Analysis:Gemini:Model"] ?? "gemini-3.5-flash";
            var geminiOptions = new GeminiAnalysisOptions(project, location, model);
            services.AddSingleton(geminiOptions);
            services.AddSingleton(_ => new Client(
                project: geminiOptions.Project,
                location: geminiOptions.Location,
                enterprise: true));
            services.AddScoped<IDocumentAnalyzer, GeminiDocumentAnalyzer>();
        }
        else
        {
            services.AddScoped<IDocumentAnalyzer, HeuristicDocumentAnalyzer>();
        }
        services.AddSingleton<CandidatePrefilter>();
        services.AddSingleton<AnalysisValidator>();
        services.AddScoped<AnalyzeRadarDocuments>();
        services.AddHttpClient<IOfficialDocumentTextSource, BoeDocumentTextSource>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("BOE-Radar-IA", "0.3"));
        });
        services.AddScoped<ImportOfficialIssue>();

        return services;
    }
}
