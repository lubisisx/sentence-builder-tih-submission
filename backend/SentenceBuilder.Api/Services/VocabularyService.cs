using SentenceBuilder.Api.Contracts;
using SentenceBuilder.Api.Interfaces;

namespace SentenceBuilder.Api.Services;

public sealed class VocabularyService(IVocabularyRepository repository)
{
    public async Task<IReadOnlyList<WordTypeResponse>> GetTypesAsync(CancellationToken ct) =>
        (await repository.GetTypesAsync(ct)).Select(x => new WordTypeResponse(x.Id, x.Name)).ToArray();

    public async Task<OperationResult<IReadOnlyList<WordResponse>>> GetWordsAsync(int typeId, CancellationToken ct)
    {
        if (typeId <= 0) return OperationResult<IReadOnlyList<WordResponse>>.Invalid("wordTypeId", "A positive wordTypeId is required.");
        if (!await repository.TypeExistsAsync(typeId, ct)) return OperationResult<IReadOnlyList<WordResponse>>.Missing();
        return OperationResult<IReadOnlyList<WordResponse>>.Success(
            (await repository.GetWordsAsync(typeId, ct)).Select(x => new WordResponse(x.Id, x.WordTypeId, x.Value)).ToArray());
    }
}
