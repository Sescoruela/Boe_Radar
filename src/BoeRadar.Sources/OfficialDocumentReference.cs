namespace BoeRadar.Sources;

public sealed record OfficialDocumentReference(string ExternalId, string Relation,
    string Description, string Direction, string OfficialUrl);
