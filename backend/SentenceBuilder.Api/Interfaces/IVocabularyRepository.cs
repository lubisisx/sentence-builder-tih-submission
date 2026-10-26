using SentenceBuilder.Api.Models;

namespace SentenceBuilder.Api.Interfaces;

public interface IVocabularyRepository
{
    Task<IReadOnlyList<WordType>> GetTypesAsync(CancellationToken cancellationToken);
    Task<bool> TypeExistsAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Word>> GetWordsAsync(int typeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Word>> FindWordsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
}
