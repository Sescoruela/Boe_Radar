namespace BoeRadar.Sources;

public sealed record ExtractedOfficialDocument(string Text, string Sha256, string Format,
    IReadOnlyList<string> Passages, string? Title, IReadOnlyList<OfficialDocumentReference> References);
