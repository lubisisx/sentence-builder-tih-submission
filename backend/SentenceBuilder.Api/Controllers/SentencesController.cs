using Microsoft.AspNetCore.Mvc;
using SentenceBuilder.Api.Contracts;
using SentenceBuilder.Api.Services;

namespace SentenceBuilder.Api.Controllers;

[ApiController]
[Route("api/sentences")]
public sealed class SentencesController(SentenceService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SentencePage>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Respond(await service.GetPageAsync(page, pageSize, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SentenceDetail>> Get(int id, CancellationToken ct) => Respond(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SentenceDetail>> Create([FromBody] SentenceRequest request, CancellationToken ct)
    {
        var result = await service.SaveAsync(null, request, ct);
        return result.Status == 200 ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : Respond(result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SentenceDetail>> Update(int id, [FromBody] SentenceRequest request, CancellationToken ct) =>
        Respond(await service.SaveAsync(id, request, ct));
}
