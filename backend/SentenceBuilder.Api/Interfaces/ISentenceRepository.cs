using SentenceBuilder.Api.Contracts;
using SentenceBuilder.Api.Models;

namespace SentenceBuilder.Api.Interfaces;

public interface ISentenceRepository
{
    Task<Sentence?> FindAsync(int id, bool tracking, CancellationToken cancellationToken);
    Task<SentencePage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    void Add(Sentence sentence);
    Task SaveAsync(CancellationToken cancellationToken);
}
