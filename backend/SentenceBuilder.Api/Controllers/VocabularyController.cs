using Microsoft.AspNetCore.Mvc;
using SentenceBuilder.Api.Contracts;
using SentenceBuilder.Api.Services;

namespace SentenceBuilder.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class VocabularyController(VocabularyService service) : ApiControllerBase
{
    [HttpGet("word-types")]
    public async Task<ActionResult<IReadOnlyList<WordTypeResponse>>> GetTypes(CancellationToken ct) => Ok(await service.GetTypesAsync(ct));

    [HttpGet("words")]
    public async Task<ActionResult<IReadOnlyList<WordResponse>>> GetWords([FromQuery] int wordTypeId, CancellationToken ct) => Respond(await service.GetWordsAsync(wordTypeId, ct));
}
