using System.Text.Json;
using BoeRadar.Application;
using BoeRadar.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoeRadar.Infrastructure.Persistence;

internal sealed class EfAnalysisCandidateStore(
    BoeRadarDbContext dbContext,
    TimeProvider timeProvider)
    : IAnalysisCandidateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AnalysisCandidate>> GetByDateAsync(
        DateOnly publicationDate,
        int limit,
        CancellationToken cancellationToken = default) =>
        await dbContext.SourceDocuments
            .AsNoTracking()
            .Where(document => document.PublicationDate == publicationDate)
            .OrderBy(document => document.ExternalId)
            .Take(limit)
            .Select(document => new AnalysisCandidate(
                document.Id,
                document.ExternalId,
                document.PublicationDate,
                document.Title,
                document.Department,
                document.SectionCode,
                document.Epigraph,
                document.OfficialXmlUrl == null ? null : new Uri(document.OfficialXmlUrl)))
            .ToArrayAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        Guid documentId,
        string contentHash,
        string modelName,
        string promptVersion,
        CancellationToken cancellationToken = default) =>
        dbContext.DocumentAnalyses.AnyAsync(
            analysis =>
                analysis.DocumentId == documentId &&
                analysis.ContentHash == contentHash &&
                analysis.ModelName == modelName &&
                analysis.PromptVersion == promptVersion,
            cancellationToken);

    public async Task SaveAsync(
        Guid documentId,
        string contentHash,
        RadarAnalysisOutput output,
        string method,
        string modelName,
        string promptVersion,
        CancellationToken cancellationToken = default)
    {
        var analysis = DocumentAnalysis.Create(
            documentId,
            contentHash,
            output.IsRelevant,
            output.Category,
            output.Summary,
            JsonSerializer.Serialize(output.Requirements, JsonOptions),
            JsonSerializer.Serialize(output.Deadlines, JsonOptions),
            JsonSerializer.Serialize(output.Evidence, JsonOptions),
            output.Confidence,
            method,
            modelName,
            promptVersion,
            output.InputTokens,
            output.OutputTokens,
            timeProvider.GetUtcNow());

        dbContext.DocumentAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
