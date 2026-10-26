using Microsoft.EntityFrameworkCore;
using SentenceBuilder.Api.Contracts;
using SentenceBuilder.Api.Interfaces;
using SentenceBuilder.Api.Models;

namespace SentenceBuilder.Api.Persistence;

public sealed class SentenceRepository(SentenceBuilderDbContext db) : ISentenceRepository
{
    public Task<Sentence?> FindAsync(int id, bool tracking, CancellationToken ct)
    {
        var query = tracking ? db.Sentences : db.Sentences.AsNoTracking();
        return query.Include(x => x.SentenceWords).SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<SentencePage> GetPageAsync(int page, int pageSize, CancellationToken ct)
    {
        var count = await db.Sentences.CountAsync(ct);
        var items = await db.Sentences.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new SentenceSummary(x.Id, x.Text, DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc), DateTime.SpecifyKind(x.UpdatedAt, DateTimeKind.Utc)))
            .ToListAsync(ct);
        return new(items, page, pageSize, count);
    }

    public void Add(Sentence sentence) => db.Sentences.Add(sentence);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
