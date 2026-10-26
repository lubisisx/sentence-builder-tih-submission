using Microsoft.EntityFrameworkCore;
using SentenceBuilder.Api.Interfaces;
using SentenceBuilder.Api.Models;

namespace SentenceBuilder.Api.Persistence;

public sealed class VocabularyRepository(SentenceBuilderDbContext db) : IVocabularyRepository
{
    public async Task<IReadOnlyList<WordType>> GetTypesAsync(CancellationToken ct) =>
        await db.WordTypes.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<bool> TypeExistsAsync(int id, CancellationToken ct) => db.WordTypes.AnyAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<Word>> GetWordsAsync(int typeId, CancellationToken ct) =>
        await db.Words.AsNoTracking().Where(x => x.WordTypeId == typeId).OrderBy(x => x.Value).ThenBy(x => x.Id).ToListAsync(ct);
    public async Task<IReadOnlyList<Word>> FindWordsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Words.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
}
