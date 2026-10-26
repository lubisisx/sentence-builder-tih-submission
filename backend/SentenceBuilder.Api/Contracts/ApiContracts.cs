namespace SentenceBuilder.Api.Contracts;

public sealed record SentenceRequest(int[]? WordIds);
public sealed record WordTypeResponse(int Id, string Name);
public sealed record WordResponse(int Id, int WordTypeId, string Text);
public sealed record SentenceWordResponse(int Position, int WordId, string Text);
public sealed record SentenceSummary(int Id, string Text, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record SentenceDetail(int Id, string Text, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IReadOnlyList<SentenceWordResponse> Words);
public sealed record SentencePage(IReadOnlyList<SentenceSummary> Items, int Page, int PageSize, int TotalCount);
