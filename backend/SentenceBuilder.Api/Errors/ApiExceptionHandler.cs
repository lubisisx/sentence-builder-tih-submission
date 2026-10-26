using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SentenceBuilder.Api.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var conflict = error is DbUpdateConcurrencyException;
        if (!conflict) logger.LogError(error, "Unhandled API failure. Trace identifier: {TraceId}", context.TraceIdentifier);
        var problem = new ProblemDetails
        {
            Status = conflict ? 409 : 500,
            Title = conflict ? "The sentence changed during this request. Reload and retry." : "An unexpected error occurred.",
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = problem.Status.Value;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
