using SentenceBuilder.Api.Contracts;
using SentenceBuilder.Api.Interfaces;
using SentenceBuilder.Api.Models;

namespace SentenceBuilder.Api.Services;

public sealed class SentenceService(ISentenceRepository sentences, IVocabularyRepository vocabulary, TimeProvider clock)
{
    public const int MaxWords = 100;
    public const int MaxTextLength = 10099;
    public const int MaxPageSize = 100;

    public async Task<OperationResult<SentenceDetail>> GetAsync(int id, CancellationToken ct)
    {
        if (id <= 0) return OperationResult<SentenceDetail>.Invalid("id", "Id must be positive.");
        var sentence = await sentences.FindAsync(id, false, ct);
        return sentence is null ? OperationResult<SentenceDetail>.Missing() : OperationResult<SentenceDetail>.Success(ToDetail(sentence));
    }

    public async Task<OperationResult<SentencePage>> GetPageAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) return OperationResult<SentencePage>.Invalid("page", "Page must be at least 1.");
        if (pageSize is < 1 or > MaxPageSize) return OperationResult<SentencePage>.Invalid("pageSize", "Page size must be between 1 and 100.");
        if ((long)(page - 1) * pageSize > int.MaxValue) return OperationResult<SentencePage>.Invalid("page", "Requested page offset is too large.");
        return OperationResult<SentencePage>.Success(await sentences.GetPageAsync(page, pageSize, ct));
    }

    public async Task<OperationResult<SentenceDetail>> SaveAsync(int? id, SentenceRequest request, CancellationToken ct)
    {
        if (id is <= 0) return OperationResult<SentenceDetail>.Invalid("id", "Id must be positive.");
        var ids = request.WordIds;
        if (ids is null || ids.Length is < 1 or > MaxWords)
            return OperationResult<SentenceDetail>.Invalid("wordIds", "Select between 1 and 100 words.");
        if (ids.Any(x => x <= 0)) return OperationResult<SentenceDetail>.Invalid("wordIds", "All word IDs must be positive.");

        Sentence? sentence = null;
        if (id.HasValue)
        {
            sentence = await sentences.FindAsync(id.Value, true, ct);
            if (sentence is null) return OperationResult<SentenceDetail>.Missing();
        }
        var words = (await vocabulary.FindWordsAsync(ids.Distinct().ToArray(), ct)).ToDictionary(x => x.Id);
        if (ids.Any(x => !words.ContainsKey(x))) return OperationResult<SentenceDetail>.Invalid("wordIds", "One or more selected words do not exist.");

        // Retain the old snapshot for each retained occurrence, even when it moves.
        var retained = (sentence?.SentenceWords ?? []).OrderBy(x => x.Position).GroupBy(x => x.WordId)
            .ToDictionary(x => x.Key, x => new Queue<string>(x.Select(w => w.WordText)));
        var snapshots = ids.Select(wordId => retained.TryGetValue(wordId, out var queue) && queue.Count > 0
            ? queue.Dequeue() : words[wordId].Value).ToArray();
        var text = string.Join(" ", snapshots);
        if (text.Length > MaxTextLength) return OperationResult<SentenceDetail>.Invalid("wordIds", "Rendered text must not exceed 10,099 UTF-16 code units.");

        var now = clock.GetUtcNow().UtcDateTime;
        if (sentence is null)
        {
            sentence = new Sentence { Text = text, CreatedAt = now, UpdatedAt = now };
            sentences.Add(sentence);
        }
        sentence.Text = text;
        sentence.UpdatedAt = now < sentence.CreatedAt ? sentence.CreatedAt : now;
        var existing = sentence.SentenceWords.ToDictionary(x => x.Position);
        foreach (var child in sentence.SentenceWords.Where(x => x.Position >= ids.Length).ToArray()) sentence.SentenceWords.Remove(child);
        for (var position = 0; position < ids.Length; position++)
        {
            if (!existing.TryGetValue(position, out var child))
            {
                child = new SentenceWord { Position = position, WordText = snapshots[position] };
                sentence.SentenceWords.Add(child);
            }
            child.WordId = ids[position];
            child.WordText = snapshots[position];
        }
        // One SaveChanges commits text, timestamp and every child mutation atomically.
        await sentences.SaveAsync(ct);
        return OperationResult<SentenceDetail>.Success(ToDetail(sentence));
    }

    private static SentenceDetail ToDetail(Sentence sentence) => new(sentence.Id, sentence.Text,
        DateTime.SpecifyKind(sentence.CreatedAt, DateTimeKind.Utc), DateTime.SpecifyKind(sentence.UpdatedAt, DateTimeKind.Utc),
        sentence.SentenceWords.OrderBy(x => x.Position).Select(x => new SentenceWordResponse(x.Position, x.WordId, x.WordText)).ToArray());
}
